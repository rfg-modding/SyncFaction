using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
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
    private readonly RecyclableMemoryStreamManager streamManager;
    private readonly ILogger<Archiver> log;

    public Archiver(IVppArchiver vppArchiver, IPegArchiver pegArchiver, ImageConverter imageConverter, XmlHelper xmlHelper, RecyclableMemoryStreamManager streamManager, ILogger<Archiver> log)
    {
        this.vppArchiver = vppArchiver;
        this.pegArchiver = pegArchiver;
        this.imageConverter = imageConverter;
        this.xmlHelper = xmlHelper;
        this.streamManager = streamManager;
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
            var isArchive = Constatns.KnownVppExtensions.Contains(extension);
            var isTextureArchive = Constatns.KnownPegExtensions.Contains(extension);
            if (isArchive)
            {
                var stream = x.OpenRead();
                return new UnpackArgs(stream, x.Name, output, matcher, settings, string.Empty);
            }

            if (isTextureArchive)
            {
                var pegFiles = PegFiles.FromExistingFile(x);
                if (pegFiles is not null)
                {
                    if (!settings.Textures.Any())
                    {
                        throw new ArgumentException("Specify at least one texture format to unpack: \"-t png\"");
                    }
                    return new UnpackArgs(pegFiles.OpenRead(), pegFiles.Cpu.Name, output, matcher, settings, string.Empty);
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
            return args.Archive switch
            {
                Stream stream => await UnpackArchiveInternal(args, stream, token),
                PegStreams pegStreams => await UnpackTexturesInternal(args, pegStreams, token),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (Exception e)
        {
            throw new Exception($"Failed {nameof(UnpackArchive)} {args}", e);
        }
    }

    /// <summary>
    /// NOTE: archive stream is disposed here! any new streams are copies (MemStreams)
    /// </summary>
    private async Task<UnpackResult> UnpackArchiveInternal(UnpackArgs args, Stream archiveStream, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (_, name, output, matcher, settings, relativePath) = args;
        var outputDir = new DirectoryInfo(Path.Combine(output.FullName, name));
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

        var hash = await Utils.ComputeHash(archiveStream);
        await using var src = archiveStream;
        var vpp = await vppArchiver.UnpackVpp(src, name, token);
        var archiveRelativePath = Path.Combine(relativePath, vpp.Name);

        var matchedFiles = matcher.Match(vpp.LogicalFiles.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", name, settings.FileGlob, matchedFiles.Count);

        var result = new List<UnpackArgs>();
        var metaEntries = new MetaEntries();
        Dictionary<string, Stream> cache = new();
        foreach (var logicalFile in vpp.LogicalFiles.Where(x => matchedFiles.Contains(x.Name)))
        {
            var outputFile = new FileInfo(Path.Combine(outputDir.FullName, logicalFile.Name));
            if (outputFile.Exists)
            {
                throw new InvalidOperationException($"File [{outputFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }
            if (!outputFile.Directory!.Exists)
            {
                throw new InvalidOperationException($"Directory [{outputFile.Directory.FullName}] doesnt exist, can not unpack. Race condition?");
            }

            var extension = Path.GetExtension(logicalFile.Name).ToLowerInvariant()[1..]; // exclude "."
            var isXml = xmlHelper.KnownXmlExtensions.Contains(extension);
            var isVpp = Constatns.KnownVppExtensions.Contains(extension);
            var isPeg = Constatns.KnownPegExtensions.Contains(extension);
            var isRegularFile = !isVpp && !isPeg;

            var tag = Path.Combine(archiveRelativePath, logicalFile.Name);
            var copyStream = streamManager.GetStream(tag, logicalFile.Content.Length);
            await logicalFile.Content.CopyToAsync(copyStream, token);
            copyStream.Seek(0, SeekOrigin.Begin);
            bool canDispose = true;

            if (isRegularFile || !settings.SkipArchives)
            {
                await ExtractFile(copyStream, isXml, outputFile, settings, token);
            }

            var eHash = await Utils.ComputeHash(copyStream);
            metaEntries.Add(logicalFile.Name, new EntryMetadata(logicalFile.Name, logicalFile.Order, logicalFile.Offset, (ulong) copyStream.Length, logicalFile.CompressedSize, eHash));

            var innerOutputDir = new DirectoryInfo(Path.Combine(outputFile.Directory.FullName, Constatns.DefaultDir));
            if (settings.Recursive && isVpp)
            {
                result.Add(new UnpackArgs(copyStream, logicalFile.Name, innerOutputDir, matcher, settings, archiveRelativePath));
                canDispose = false;
            }

            if (settings.Textures.Any() && isPeg)
            {
                cache.Add(logicalFile.Name, copyStream);
                canDispose = false;

                var pegStreams = FindPegEntryPair(logicalFile.Name, cache);
                if (pegStreams is not null)
                {
                    result.Add(new UnpackArgs(pegStreams, logicalFile.Name, innerOutputDir, matcher, settings, archiveRelativePath));
                    cache.Remove(PegFiles.GetCpuFileName(logicalFile.Name)!);
                    cache.Remove(PegFiles.GetGpuFileName(logicalFile.Name)!);
                }
            }

            if (canDispose)
            {
                await copyStream.DisposeAsync();
            }
        }

        if (cache.Any())
        {
            var items = string.Join(", ", cache.Keys);
            throw new InvalidOperationException($"Some items failed to extract properly from {archiveRelativePath}: [{items}]");
        }

        var archiveMetadata = new ArchiveMetadata(vpp.Name, vpp.Mode.ToString(), archiveStream.Length.ToString(), (ulong) matchedFiles.Count, hash, metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, args, result);
    }

    public static PegStreams? FindPegEntryPair(string name, IReadOnlyDictionary<string, Stream> cache)
    {
        var cpu = PegFiles.GetCpuFileName(name);
        var gpu = PegFiles.GetGpuFileName(name);
        var cpuStream = cache.GetValueOrDefault(cpu);
        var gpuStream = cache.GetValueOrDefault(gpu);
        if (cpuStream is null || gpuStream is null)
        {
            return null;
        }

        return new PegStreams(cpuStream, gpuStream);
    }

    private async Task<UnpackResult> UnpackTexturesInternal(UnpackArgs args, PegStreams pegStreams, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (_, name, output, matcher, settings, relativePath) = args;
        var outputDir = new DirectoryInfo(Path.Combine(output.FullName, name));
        var pegNameNoExt = Path.GetFileNameWithoutExtension(name);
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

        var pegHash = await Utils.ComputeHash(pegStreams);
        await using var streams = pegStreams;
        var peg = await pegArchiver.UnpackPeg(streams, pegNameNoExt, token);
        var archiveRelativePath = Path.Combine(relativePath, peg.Name);

        var matchedFiles = matcher.Match(peg.LogicalTextures.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", name, settings.FileGlob, matchedFiles.Count);

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
        var archiveMetadata = new ArchiveMetadata(peg.Name, "peg", pegStreams.Size, (ulong) matchedFiles.Count, pegHash, metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, args, result);
    }

    private async Task ExtractFile(MemoryStream content, bool isXml, FileInfo outputFile, UnpackSettings settings, CancellationToken token)
    {
        await using var fileStream = outputFile.OpenWrite();
        if (settings.XmlFormat && isXml)
        {
            // reformat original xml for readability
            var xml = new XmlDocument();
            using var reader = new StreamReader(content);
            xml.Load(reader);
            using var ms = new MemoryStream();
            xml.SerializeToMemoryStream(ms, true);
            await ms.CopyToAsync(fileStream, token);
        }
        else
        {
            await content.CopyToAsync(fileStream, token);
        }
        outputFile.Refresh();
        content.Seek(0, SeekOrigin.Begin);
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

        logicalTexture.Data.Seek(0, SeekOrigin.Begin);
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
