using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.ModManager;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Models;
using SyncFaction.Packer.Services;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public class Archiver
{
    private readonly IVppArchiver vppArchiver;
    private readonly XmlHelper xmlHelper;
    private readonly ILogger<Archiver> log;

    public Archiver(IVppArchiver vppArchiver, XmlHelper xmlHelper, ILogger<Archiver> log)
    {
        this.vppArchiver = vppArchiver;
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
        var batchSize = 100;
        while (tasks.Any())
        {
            var batch = tasks.Take(batchSize).ToList();
            var completed = await Task.WhenAny(batch);
            var result = await completed;
            metadata.Add(result.RelativePath, result.ArchiveMetadata);
            tasks.AddRange(result.MoreTasks);
            tasks.Remove(completed);
        }

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

        string hash;
        await using (var s = archive.OpenRead())
        {
            hash = ComputeHash(s);
        }

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

            var extension = logicalFile.Name.ToLowerInvariant().Split('.').Last();
            var isXml = xmlHelper.KnownXmlExtensions.Contains(extension);
            var isArchive = KnownArchiveExtensions.Contains(extension);

            await ExtractFile(logicalFile, isXml, outputFile, settings, token);
            outputFile.Refresh();
            await using var s = outputFile.OpenRead();
            var eHash = ComputeHash(s);
            metaEntries.Add(logicalFile.Name, new EntryMetadata(logicalFile.Name, logicalFile.Order, logicalFile.Offset, (ulong) logicalFile.Content.Length, logicalFile.CompressedSize, eHash));

            var innerOutputDir = new DirectoryInfo(Path.Combine(outputFile.Directory.FullName, DefaultDir, outputFile.Name));
            if (settings.Recursive && isArchive)
            {
                var task = UnpackArchive(outputFile, innerOutputDir, matcher, settings, archiveRelativePath, token);
                tasks.Add(task);
            }
        }

        var archiveMetadata = new ArchiveMetadata(vpp.Name, vpp.Mode.ToString(), (ulong) archive.Length, (ulong) matchedFiles.Count, hash, metaEntries);
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

    public static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashValue = sha.ComputeHash(stream);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }

    public const string DefaultDir = ".unpack";
    public const string MetadataFile = ".metadata";

    public static readonly ImmutableHashSet<string> KnownArchiveExtensions = new HashSet<string>
    {
        "vpp",
        "vpp_pc",
        "str2",
        "str2_pc"
    }.ToImmutableHashSet();

    private record UnpackResult(string RelativePath, ArchiveMetadata ArchiveMetadata, IReadOnlyList<Task<UnpackResult>> MoreTasks);
}
