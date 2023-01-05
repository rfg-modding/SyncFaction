using System.Text.Json;
using FluentAssertions;
using Kaitai;
using SyncFaction.Packer;

namespace SyncFactionTests.Packer;

public class ArtifactGenerators
{
    [Explicit("Creates new vpp archives with same flags and contents. Repacks str2 files too")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public async Task RepackAll(FileInfo fileInfo)
    {
        await using var fileStream = fileInfo.OpenRead();
        var archive = Tools.UnpackVpp(fileStream, fileInfo.Name);
        var patchedFiles = PatchFiles(archive.LogicalFiles);
        var patched = archive with {LogicalFiles = patchedFiles};

        var dir = new DirectoryInfo(Path.Combine(TestUtils.ArtifactDir.FullName, "repack"));
        dir.Create();
        var dstFile = new FileInfo(Path.Combine(dir.FullName, fileInfo.Name));
        dstFile.Delete();
        await using var dstStream = dstFile.OpenWrite();
        var writer = new VppWriter(patched, CancellationToken.None);
        await writer.WriteAll(dstStream);
    }

    [Explicit("For debugging")]
    [TestCase("edfbarricade_vehicle_a.rfgchunkx.str2_pc")]
    [TestCase("missing_chunk.rfgchunkx.str2_pc")]
    [TestCase("missing_destroyable.rfgchunkx.str2_pc")]
    public void UnpackStr2(string name)
    {
        var file = new FileInfo(Path.Combine(TestUtils.UnpackDir.FullName, name));
        UnpackVpp(file);
    }

    [Explicit("Unpacks files from original vpp files into subdirectories")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void UnpackVpp(FileInfo fileInfo)
    {
        using var stream = fileInfo.OpenRead();
        var archive = Tools.UnpackVpp(stream, fileInfo.Name);
        var subdir = TestUtils.UnpackDir.CreateSubdirectory("_" + fileInfo.Name);
        subdir.Delete(true);
        subdir.Create();
        foreach (var logicalFile in archive.LogicalFiles)
        {
            var dstFile = Path.Combine(subdir.FullName, $"{logicalFile.Order:D5}_" + logicalFile.Name);
            File.WriteAllBytes(dstFile, logicalFile.Content);

            if (logicalFile.Name.ToLower().EndsWith(".str2_pc"))
            {
                // we need to go deeper. run other tests again to see how they process extracted files
            }
        }
        Assert.Pass();
    }

    [Explicit("Recursively calclates hashes for each entry in game archives. Result is stored in hashes.json")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void CalculateHashes(FileInfo fileInfo)
    {
        if (fileInfo.Extension.ToLower() != ".vpp_pc")
        {
            Assert.Ignore("This test starts from base vpp archives");
        }

        var key = fileInfo.Name;
        using var fileStream = fileInfo.OpenRead();
        HashRecursive(fileStream, key, null);
    }

    public void HashRecursive(Stream stream, string name, string? parentKey)
    {
        var key = parentKey == null ? name : $"{parentKey}/{name}";
        Console.WriteLine($"hash {key}");

        var hashString = TestUtils.ComputeHash(stream);
        stream.Position = 0;
        if (name.EndsWith(".str2_pc") || name.EndsWith(".vpp_pc"))
        {
            // this is just for reading flags. actual data is read later
            var vpp = new RfgVpp(new KaitaiStream(stream));
            var alignment = vpp.DetectAlignmentSize();
            var zlibInfo = "none";
            if (vpp.Header.Flags.Mode is RfgVpp.HeaderBlock.Mode.Compressed or RfgVpp.HeaderBlock.Mode.Compacted && vpp.Entries.Any())
            {
                var compressionLevel = vpp.DetectCompressionLevel();
                var zlib = vpp.BlockCompactData?.ZlibHeader ?? vpp.BlockEntryData.Value.First().Value.ZlibHeader;
                zlibInfo = $"{zlib}/{compressionLevel}";
            }


            hashString += $", compressed={vpp.Header.Flags.Compressed}, condensed={vpp.Header.Flags.Condensed}, alignment={alignment}, entries={vpp.Entries.Count}, zlib={zlibInfo}";
        }
        AllHashes[key] = hashString;
        stream.Position = 0;

        if (name.EndsWith(".str2_pc") || name.EndsWith(".vpp_pc"))
        {
            Console.WriteLine($"read {key}");
            var entries = Tools.UnpackVpp(stream, key).LogicalFiles;
            foreach (var entry in entries)
            {
                using var ms = new MemoryStream(entry.Content);
                HashRecursive(ms, entry.Name, key);
            }
        }
    }

    public static IEnumerable<LogicalFile> PatchFiles(IEnumerable<LogicalFile> files)
    {
        foreach (var logicalFile in files)
        {
            if (Path.GetExtension(logicalFile.Name).ToLower() == ".str2_pc")
            {
                var repackStr2 = RepackStr2(logicalFile);
                yield return logicalFile with {Content = repackStr2};
            }
            else
            {
                yield return logicalFile;
            }
        }
    }

    private static byte[] RepackStr2(LogicalFile logicalFile)
    {
        Console.WriteLine($"repacking {logicalFile.Name}");
        var ms = new MemoryStream(logicalFile.Content);
        var archive = Tools.UnpackVpp(ms, logicalFile.Name);
        using var dstStream = new MemoryStream();
        var writer = new VppWriter(archive, CancellationToken.None);
        writer.WriteAll(dstStream).GetAwaiter().GetResult();
        return dstStream.ToArray();
    }

    public static readonly Dictionary<string,string> AllHashes = new();

    [OneTimeSetUp]
    public void Init()
    {
        try
        {
            TestUtils.ArtifactDir.Create();
            TestUtils.UnpackDir.Create();
        }
        catch
        {
            // ignored exceptions when steam game is not installed
        }
    }

    [OneTimeTearDown]
    public void WriteHashes()
    {
        if (AllHashes.Any())
        {
            File.WriteAllText(TestUtils.ArtifactDir + @"\hashes.json", JsonSerializer.Serialize(AllHashes, new JsonSerializerOptions() {WriteIndented = true}));
        }
    }
}
