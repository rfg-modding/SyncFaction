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
        var tasks = archivePaths.Select(archivePath => new FileInfo(archivePath)).Select(x => UnpackArchive(x, output, matcher, settings, string.Empty, cts.Token)).ToList();

        //var batchSize = Environment.ProcessorCount;
        var batchSize = 1;
        while (tasks.Any())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var batch = tasks.Take(batchSize).ToList();
                var completed = await Task.WhenAny(batch);
                var result = await completed;
                metadata.Add(result.RelativePath, result.ArchiveMetadata);
                tasks.AddRange(result.MoreTasks);
                tasks.Remove(completed);
            }
            catch (Exception e)
            {
                cts.Cancel();
                tasks.Clear();
                log.LogError("Tasks canceled because of exception");
                throw;
            }
        }

        await Task.WhenAll(tasks);

        if (settings.Metadata)
        {
            //var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions {WriteIndented = true});
            var serialized = Serialize(metadata);
            var metaFile = new FileInfo(Path.Combine(output.FullName, MetadataFile));
            await File.WriteAllTextAsync(metaFile.FullName, serialized, cts.Token);
            log.LogInformation("Metadata saved to {file}", metaFile.FullName);
        }

        log.LogInformation("Completed in {time}", sw.Elapsed);
    }

    private string Serialize(Metadata metadata)
    {
        var sb = new StringBuilder();
        foreach (var (key, (name, mode, size, entryCount, hash, metaEntries)) in metadata)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{key}\t\t{mode}, {entryCount} entries, {size} bytes, {hash}");
            foreach (var (eKey, (eName, order, offset, eSize, compressedSize, eHash)) in metaEntries)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{key}\\{eKey}\t\t{order}, {eSize} bytes, {eHash}");
            }
        }

        return sb.ToString();
    }

    private async Task<UnpackResult> UnpackArchive(FileInfo archive, DirectoryInfo output, Matcher matcher, UnpackSettings settings, string relativePath, CancellationToken token)
    {
        try
        {
            return await UnpackArchiveInternal(archive, output, matcher, settings, relativePath, token);
        }
        catch (Exception e)
        {
            throw new Exception($"Failed {nameof(UnpackArchive)}({archive.FullName}, {output.FullName}, {matcher}, {settings}, {relativePath}, token)", e);
        }
    }

    private async Task<UnpackResult> UnpackTextures(FileInfo archive, DirectoryInfo output, Matcher matcher, UnpackSettings settings, string relativePath, CancellationToken token)
    {
        try
        {
            return await UnpackTexturesInternal(archive, output, matcher, settings, relativePath, token);
        }
        catch (Exception e)
        {
            throw new Exception($"Failed {nameof(UnpackTextures)}({archive.FullName}, {output.FullName}, {matcher}, {settings}, {relativePath}, token)", e);
        }
    }

    private async Task<UnpackResult> UnpackArchiveInternal(FileInfo archive, DirectoryInfo output, Matcher matcher, UnpackSettings settings, string relativePath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
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

        var hash = await ComputeHash(archive);
        await using var src = archive.OpenRead();
        var vpp = await vppArchiver.UnpackVpp(src, archive.Name, token);
        var archiveRelativePath = Path.Combine(relativePath, vpp.Name);

        var matchedFiles = matcher.Match(vpp.LogicalFiles.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", archive.Name, settings.FileGlob, matchedFiles.Count);

        var tasks = new List<Task<UnpackResult>>();
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
            var isArchive = KnownArchiveExtensions.Contains(extension);
            var isTextureArchive = KnownTextureArchiveExtensions.Contains(extension);

            await ExtractFile(logicalFile, isXml, outputFile, settings, token);
            outputFile.Refresh();
            var eHash = await ComputeHash(outputFile);
            metaEntries.Add(logicalFile.Name, new EntryMetadata(logicalFile.Name, logicalFile.Order, logicalFile.Offset, (ulong) logicalFile.Content.Length, logicalFile.CompressedSize, eHash));

            var innerOutputDir = new DirectoryInfo(Path.Combine(outputFile.Directory.FullName, DefaultDir));
            if (settings.Recursive && isArchive)
            {
                var task = UnpackArchive(outputFile, innerOutputDir, matcher, settings, archiveRelativePath, token);
                tasks.Add(task);
            }

            if (settings.Textures && isTextureArchive)
            {
                // TODO possible race condition and unpack starts twice?
                var (cpu, gpu) = pegArchiver.GetPairFiles(outputFile);
                if (cpu is not null && gpu is not null)
                {
                    var task = UnpackTextures(outputFile, innerOutputDir, matcher, settings, archiveRelativePath, token);
                    tasks.Add(task);
                }
            }
        }

        var archiveMetadata = new ArchiveMetadata(vpp.Name, vpp.Mode.ToString(), (ulong) archive.Length, (ulong) matchedFiles.Count, hash, metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, tasks);
    }

    private async Task<UnpackResult> UnpackTexturesInternal(FileInfo archive, DirectoryInfo output, Matcher matcher, UnpackSettings settings, string relativePath, CancellationToken token)
    {
        var outputDir = new DirectoryInfo(Path.Combine(output.FullName, archive.Name));
        var (cpu, gpu) = pegArchiver.GetPairFiles(archive);
        if (cpu is null || gpu is null)
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

        var cpuHash = await ComputeHash(cpu);
        var gpuHash = await ComputeHash(gpu);
        await using var c = cpu.OpenRead();
        await using var g = gpu.OpenRead();
        var peg = await pegArchiver.UnpackPeg(c, g, pegNameNoExt, token);
        var archiveRelativePath = Path.Combine(relativePath, peg.Name);

        var matchedFiles = matcher.Match(peg.LogicalTextures.Select(x => x.Name)).Files.Select(x => x.Path).ToHashSet();
        log.LogInformation("[{archive}]: [{fileGlob}] matched {count} files", archive.Name, settings.FileGlob, matchedFiles.Count);

        var tasks = new List<Task<UnpackResult>>();
        var metaEntries = new MetaEntries();
        foreach (var logicalTexture in peg.LogicalTextures.Where(x => matchedFiles.Contains(x.Name)))
        {
            var fileName = Path.GetFileNameWithoutExtension(logicalTexture.Name);
            // NOTE: names are non-unique
            var name = $"{logicalTexture.Order:D4} {fileName}";
            var outputFile = new FileInfo($"{Path.Combine(outputDir.FullName, name)}.dds");
            if (outputFile.Exists)
            {
                throw new InvalidOperationException($"File [{outputFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }

            await ExtractRawTexture(logicalTexture, outputFile, token);
            outputFile.Refresh();
            var dHash = await ComputeHash(outputFile);
            metaEntries.Add(name, new EntryMetadata(outputFile.Name, logicalTexture.Order, (ulong)logicalTexture.DataOffset, (ulong) logicalTexture.Data.Length, 0, dHash));

            logicalTexture.Data.Seek(0, SeekOrigin.Begin);
            var pngFile = new FileInfo($"{Path.Combine(outputDir.FullName, name)}.png");
            if (pngFile.Exists)
            {
                throw new InvalidOperationException($"File [{pngFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }
            await ExtractPngTexture(logicalTexture, pngFile, token);
            pngFile.Refresh();
            var pHash = await ComputeHash(pngFile);
            metaEntries.Add(pngFile.Name, new EntryMetadata(pngFile.Name, logicalTexture.Order, 0, (ulong) pngFile.Length, 0, pHash));
        }
        var archiveMetadata = new ArchiveMetadata(peg.Name, "texture", (ulong) archive.Length, (ulong) matchedFiles.Count, $"{cpuHash}_{gpuHash}", metaEntries);
        return new UnpackResult(archiveRelativePath, archiveMetadata, tasks);
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
    }

    private async Task ExtractRawTexture(LogicalTexture logicalTexture, FileInfo outputFile, CancellationToken token)
    {
        await using var fileStream = outputFile.OpenWrite();
        var header = await imageConverter.BuildHeader(logicalTexture, token);
        await header.CopyToAsync(fileStream, token);
        await logicalTexture.Data.CopyToAsync(fileStream, token);
    }

    private async Task ExtractPngTexture(LogicalTexture logicalTexture, FileInfo outputFile, CancellationToken token)
    {
        await using var fileStream = outputFile.OpenWrite();
        var image = imageConverter.DecodeFirstFrame(logicalTexture);
        await imageConverter.WritePngFile(image, fileStream, token);
    }

    public static async Task<string> ComputeHash(FileInfo file)
    {
        await using var s = file.OpenRead();
        return ComputeHash(s);
    }

    public static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashValue = sha.ComputeHash(stream);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }

    public const string DefaultDir = ".unpack";
    public const string DefaultOutputDir = ".output";
    public const string MetadataFile = ".metadata";

    public static readonly ImmutableHashSet<string> KnownArchiveExtensions = new HashSet<string>
    {
        "vpp",
        "vpp_pc",
        "str2",
        "str2_pc"
    }.ToImmutableHashSet();

    public static readonly ImmutableHashSet<string> KnownTextureArchiveExtensions = new HashSet<string>
    {
        "cpeg_pc",
        "cvbm_pc",
        "gpeg_pc",
        "gvbm_pc",
    }.ToImmutableHashSet();

    private record UnpackResult(string RelativePath, ArchiveMetadata ArchiveMetadata, IReadOnlyList<Task<UnpackResult>> MoreTasks);
}
