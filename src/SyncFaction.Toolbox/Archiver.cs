using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Models;
using SyncFaction.Packer.Models.Peg;
using SyncFaction.Packer.Services;
using SyncFaction.Packer.Services.Peg;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public class Archiver
{
    private readonly IVppArchiver vppArchiver;
    private readonly IPegArchiver pegArchiver;
    private readonly ImageConverter imageConverter;
    private readonly XmlHelper xmlHelper;
    private readonly ILogger<Archiver> log;

    public Archiver(IVppArchiver vppArchiver, IPegArchiver pegArchiver, ImageConverter imageConverter, XmlHelper xmlHelper, ILogger<Archiver> log)
    {
        this.vppArchiver = vppArchiver;
        this.pegArchiver = pegArchiver;
        this.imageConverter = imageConverter;
        this.xmlHelper = xmlHelper;
        this.log = log;
    }

    public async Task Unpack(UnpackSettings settings, CancellationToken token)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(settings.FileGlob);
        var output = new DirectoryInfo(settings.OutputPath);
        if (!output.Exists)
        {
            output.Create();
            output.Refresh();
        }

        var archiveMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        archiveMatcher.AddInclude(settings.ArchiveGlob);
        // TODO what if glob has absolute path?
        var archivePaths = archiveMatcher.GetResultsInFullPath(Directory.GetCurrentDirectory()).ToList();
        log.LogInformation("Unpacking: [{archive}], found {count} archive(s)", settings.ArchiveGlob, archivePaths.Count);
        log.LogInformation("Output: [{output}]", output.FullName);
        log.LogInformation("Flags: xmlFormat={xmlFormat} force={force}", settings.XmlFormat, settings.Force);

        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var metadata = new Metadata();
        var unpackArgsQueue = archivePaths.Select(x => new FileInfo(x)).Select(x =>
        {
            var extension = x.Name.ToLowerInvariant().Split('.').Last();
            var isArchive = Constatns.KnownArchiveExtensions.Contains(extension);
            var isTextureArchive = Constatns.KnownTextureArchiveExtensions.Contains(extension);
            if (isArchive)
            {
                return new UnpackArgs(ArchiveType.Vpp, x, output, matcher, settings, string.Empty);
            }

            if (isTextureArchive)
            {
                // unpack explicitly given pegs, even if -t is not specified
                var pegFiles = PegFiles.FromExistingFile(x);
                if (pegFiles is not null)
                {
                    return new UnpackArgs(ArchiveType.Peg, pegFiles.Cpu, output, matcher, settings, string.Empty);
                }
            }

            throw new InvalidOperationException($"Unknown archive type [{x.FullName}]");
        }).ToList();
        var runningTasks = new Dictionary<UnpackArgs, Task<UnpackResult>>();
        var batchSize = settings.Parallel;
        while (unpackArgsQueue.Any())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var batch = unpackArgsQueue.Take(batchSize).ToList();
                foreach (var x in batch.Where(x => !runningTasks.ContainsKey(x)))
                {
                    runningTasks.Add(x, UnpackArchive(x, cts.Token));
                }
                var completed = await Task.WhenAny(runningTasks.Values);
                var result = await completed;
                metadata.Add(result.RelativePath, result.ArchiveMetadata);
                unpackArgsQueue.AddRange(result.More);
                unpackArgsQueue.Remove(result.Args);
                runningTasks.Remove(result.Args);
            }
            catch (Exception e)
            {
                cts.Cancel();
                unpackArgsQueue.Clear();
                log.LogError("Tasks canceled because of exception");
                throw;
            }
        }


        await Task.WhenAll(runningTasks.Values);

        if (settings.Metadata)
        {
            //var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions {WriteIndented = true});
            var serialized = metadata.Serialize();
            var metaFile = new FileInfo(Path.Combine(output.FullName, Constatns.MetadataFile));
            await File.WriteAllTextAsync(metaFile.FullName, serialized, cts.Token);
            log.LogInformation("Metadata saved to {file}", metaFile.FullName);
        }

        log.LogInformation("Completed in {time}", sw.Elapsed);
    }

    private async Task<UnpackResult> UnpackArchive(UnpackArgs args, CancellationToken token)
    {
        try
        {
            return args.Type switch
            {
                ArchiveType.Vpp => await UnpackArchiveInternal(args, token),
                ArchiveType.Peg => await UnpackTexturesInternal(args, token),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (Exception e)
        {
            throw new Exception($"Failed {nameof(UnpackArchive)}({args.Type}, {args.Archive.FullName}, {args.Output.FullName}, {args.Settings}, {args.RelativePath})", e);
        }
    }

    private async Task<UnpackResult> UnpackArchiveInternal(UnpackArgs args, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (_, archive, output, matcher, settings, relativePath) = args;
        var outputDir = new DirectoryInfo(Path.Combine(output.FullName, archive.Name));
        if (outputDir.Exists)
        {
            if (outputDir.EnumerateFileSystemInfos().Any() && !settings.Force)
            {
                throw new ArgumentException($"Output directory [{outputDir.FullName}] is not empty. Use --force to overwrite");
            }

            outputDir.Delete(true);
            outputDir.Refresh();
        }

        outputDir.Create();
        outputDir.Refresh();

        var hash = await Utils.ComputeHash(archive);
        await using var src = archive.OpenRead();
        var vpp = await vppArchiver.UnpackVpp(src, archive.Name, token);
        var archiveRelativePath = Path.Combine(relativePath, vpp.Name);

        var matchedFiles = matcher.Match(vpp.LogicalFiles.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", archive.Name, settings.FileGlob, matchedFiles.Count);

        var result = new List<UnpackArgs>();
        var metaEntries = new MetaEntries();
        foreach (var logicalFile in vpp.LogicalFiles.Where(x => matchedFiles.Contains(x.Name)))
        {
            var outputFile = new FileInfo(Path.Combine(outputDir.FullName, logicalFile.Name));
            if (outputFile.Exists)
            {
                throw new InvalidOperationException($"File [{outputFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }
            if (!outputFile.Directory.Exists)
            {
                throw new InvalidOperationException($"Directory [{outputFile.Directory.FullName}] doesnt exist, can not unpack. Race condition?");
            }

            var extension = logicalFile.Name.ToLowerInvariant().Split('.').Last();
            var isXml = xmlHelper.KnownXmlExtensions.Contains(extension);
            var isArchive = Constatns.KnownArchiveExtensions.Contains(extension);
            var isTextureArchive = Constatns.KnownTextureArchiveExtensions.Contains(extension);

            await ExtractFile(logicalFile, isXml, outputFile, settings, token);
            var eHash = await Utils.ComputeHash(outputFile);
            metaEntries.Add(logicalFile.Name, new EntryMetadata(logicalFile.Name, logicalFile.Order, logicalFile.Offset, (ulong) logicalFile.Content.Length, logicalFile.CompressedSize, eHash));

            var innerOutputDir = new DirectoryInfo(Path.Combine(outputFile.Directory.FullName, Constatns.DefaultDir));
            if (settings.Recursive && isArchive)
            {
                result.Add(new UnpackArgs(ArchiveType.Vpp, outputFile, innerOutputDir, matcher, settings, archiveRelativePath));
            }

            if (settings.Textures.Any() && isTextureArchive)
            {
                // NOTE: no race condition because key is always "cpu" file from the pair and tasks are created per unique args key
                var pegFiles = PegFiles.FromExistingFile(outputFile);
                if (pegFiles is not null)
                {
                    result.Add(new UnpackArgs(ArchiveType.Peg, pegFiles.Cpu, innerOutputDir, matcher, settings, archiveRelativePath));
                }
            }
        }

        var archiveMetadata = new ArchiveMetadata(vpp.Name, vpp.Mode.ToString(), (ulong) archive.Length, (ulong) matchedFiles.Count, hash, metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, args, result);
    }

    private async Task<UnpackResult> UnpackTexturesInternal(UnpackArgs args, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (_, archive, output, matcher, settings, relativePath) = args;
        var outputDir = new DirectoryInfo(Path.Combine(output.FullName, archive.Name));
        var pegFiles = PegFiles.FromExistingFile(archive);
        if (pegFiles is null)
        {
            throw new ArgumentException($"PEG does not have a pair: [{archive.FullName}]");
        }

        var pegNameNoExt = Path.GetFileNameWithoutExtension(archive.Name);
        if (outputDir.Exists)
        {
            if (outputDir.EnumerateFileSystemInfos().Any() && !settings.Force)
            {
                throw new ArgumentException($"Output directory [{outputDir.FullName}] is not empty. Use --force to overwrite");
            }

            outputDir.Delete(true);
            outputDir.Refresh();
        }

        outputDir.Create();
        outputDir.Refresh();

        var pegHash = await Utils.ComputeHash(pegFiles);
        await using var streams = pegFiles.OpenRead();
        var peg = await pegArchiver.UnpackPeg(streams, pegNameNoExt, token);
        var archiveRelativePath = Path.Combine(relativePath, peg.Name);

        var matchedFiles = matcher.Match(peg.LogicalTextures.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", archive.Name, settings.FileGlob, matchedFiles.Count);

        // NOTE: peg containers are not expected to have nested stuff
        var result = new List<UnpackArgs>();
        var metaEntries = new MetaEntries();
        foreach (var logicalTexture in peg.LogicalTextures.Where(x => matchedFiles.Contains(x.Name)))
        {
            foreach (var textureFormat in args.Settings.Textures)
            {
                token.ThrowIfCancellationRequested();
                var outputFile = await ExtractTexture(logicalTexture, textureFormat, outputDir, token);
                var hash = await Utils.ComputeHash(outputFile);
                metaEntries.Add(outputFile.Name, new EntryMetadata(outputFile.Name, logicalTexture.Order, (ulong)logicalTexture.DataOffset, (ulong) logicalTexture.Data.Length, 0, hash));
            }
        }
        var archiveMetadata = new ArchiveMetadata(peg.Name, "peg", (ulong) archive.Length, (ulong) matchedFiles.Count, pegHash, metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, args, result);
    }

    private async Task ExtractFile(LogicalFile logicalFile, bool isXml, FileInfo outputFile, UnpackSettings settings, CancellationToken token)
    {
        await using var fileStream = outputFile.OpenWrite();
        if (settings.XmlFormat && isXml)
        {
            // reformat original xml for readability
            var xml = new XmlDocument();
            using var reader = new StreamReader(logicalFile.Content);
            xml.Load(reader);
            using var ms = new MemoryStream();
            xml.SerializeToMemoryStream(ms, true);
            await ms.CopyToAsync(fileStream, token);
        }
        else
        {
            await logicalFile.Content.CopyToAsync(fileStream, token);
        }
        outputFile.Refresh();
    }

    private async Task<FileInfo> ExtractTexture(LogicalTexture logicalTexture, TextureFormat format, DirectoryInfo outputDir, CancellationToken token)
    {
        var fileName = Path.GetFileNameWithoutExtension(logicalTexture.Name);
        // NOTE: names are non-unique
        var name = $"{logicalTexture.Order:D4} {fileName}";
        var outputFile = new FileInfo($"{Path.Combine(outputDir.FullName, name)}.{format.ToString().ToLowerInvariant()}");
        if (outputFile.Exists)
        {
            throw new InvalidOperationException($"File [{outputFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
        }
        await using var output = outputFile.OpenWrite();
        switch(format)
        {
            case TextureFormat.DDS:
                var header = await imageConverter.BuildHeader(logicalTexture, token);
                await header.CopyToAsync(output, token);
                await logicalTexture.Data.CopyToAsync(output, token);
                break;
            case TextureFormat.PNG:
                var image = imageConverter.DecodeFirstFrame(logicalTexture);
                await imageConverter.WritePngFile(image, output, token);
                break;
            case TextureFormat.RAW:
                await logicalTexture.Data.CopyToAsync(output, token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);
        outputFile.Refresh();
        return outputFile;
    }
}
