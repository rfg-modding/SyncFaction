using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;
using SyncFaction.Packer.Models;
using SyncFaction.Packer.Services;
using SyncFactionTests.VppRam;

namespace SyncFactionTests.Packer;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tests")]
public class ArtifactGenerators
{
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
            System.IO.File.WriteAllText(TestUtils.ArtifactDir + @"\hashes.json", JsonSerializer.Serialize(AllHashes, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Explicit("Creates new vpp archives with same flags and contents. Repacks str2 files too")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public async Task RepackAll(FileInfo fileInfo)
    {
        var dir = new DirectoryInfo(Path.Combine(TestUtils.ArtifactDir.FullName, "repack"));
        dir.Create();
        var dstFile = new FileInfo(Path.Combine(dir.FullName, fileInfo.Name));
        dstFile.Delete();

        await using var fileStream = fileInfo.OpenRead();
        var archive = new VppInMemoryReader().Read(fileStream, fileInfo.Name, CancellationToken.None);
        var patchedFiles = PatchFiles(archive.LogicalFiles);
        var patched = archive with { LogicalFiles = patchedFiles };

        await using var dstStream = dstFile.OpenWrite();
        var writer = new VppInMemoryWriter(patched);
        await writer.WriteAll(dstStream, CancellationToken.None);
    }

    [Explicit("Creates new vpp archives with same flags and contents. Repacks str2 files too")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public async Task CompareRepackAll(FileInfo fileInfo)
    {
        var dir = new DirectoryInfo(Path.Combine(TestUtils.ArtifactDir.FullName, "repack"));
        dir.Create();
        var dstFileInMemory = new FileInfo(Path.Combine(dir.FullName, fileInfo.Name + ".inmemory"));
        var dstFileStreamed = new FileInfo(Path.Combine(dir.FullName, fileInfo.Name + ".streamed"));
        dstFileInMemory.Delete();
        dstFileStreamed.Delete();

        //===============================

        await using var fileStream1 = fileInfo.OpenRead();
        var archive1 = new VppInMemoryReader().Read(fileStream1, fileInfo.Name, CancellationToken.None);
        var patchedFiles1 = PatchFiles(archive1.LogicalFiles);
        var patched1 = archive1 with { LogicalFiles = patchedFiles1 };

        await using (var dstStream1 = dstFileInMemory.OpenWrite())
        {
            var writer1 = new VppInMemoryWriter(patched1);
            await writer1.WriteAll(dstStream1, CancellationToken.None);
        }

        //===============================

        await using var fileStream2 = fileInfo.OpenRead();
        var archive2 = new VppReader().Read(fileStream2, fileInfo.Name, CancellationToken.None);
        var patchedFiles2 = PatchFiles(archive2.LogicalFiles);
        var patched2 = archive2 with { LogicalFiles = patchedFiles2 };

        await using (var dstStream2 = dstFileStreamed.OpenWrite())
        {
            var writer2 = new VppWriter(patched2);
            await writer2.WriteAll(dstStream2, CancellationToken.None);
        }

        //===============================

        dstFileInMemory.Refresh();
        dstFileStreamed.Refresh();

        dstFileStreamed.Length.Should().Be(dstFileInMemory.Length);

        //===============================
        string hashInMemory;
        string hashStreamed;

        using var sha1 = SHA256.Create();
        using (var readStream1 = dstFileInMemory.Open(FileMode.Open))
        {
            readStream1.Position = 0;
            var hashValue1 = sha1.ComputeHash(readStream1);
            hashInMemory = BitConverter.ToString(hashValue1).Replace("-", "");
        }

        using var sha2 = SHA256.Create();
        using (var readStream2 = dstFileStreamed.Open(FileMode.Open))
        {
            readStream2.Position = 0;
            var hashValue2 = sha2.ComputeHash(readStream2);
            hashStreamed = BitConverter.ToString(hashValue2).Replace("-", "");
        }

        //===============================

        hashStreamed.Should().Be(hashInMemory);

        dstFileInMemory.Delete();
        dstFileStreamed.Delete();
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
        var archive = new VppInMemoryReader().Read(stream, fileInfo.Name, CancellationToken.None);
        var subdir = TestUtils.UnpackDir.CreateSubdirectory("_" + fileInfo.Name);
        subdir.Delete(true);
        subdir.Create();
        foreach (var logicalFile in archive.LogicalFiles)
        {
            var dstFile = Path.Combine(subdir.FullName, $"{logicalFile.Order:D5}_" + logicalFile.Name);
            System.IO.File.WriteAllBytes(dstFile, logicalFile.Content);

            if (logicalFile.Name.ToLowerInvariant().EndsWith(".str2_pc", StringComparison.OrdinalIgnoreCase))
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
        if (fileInfo.Extension.ToLowerInvariant() != ".vpp_pc")
        {
            Assert.Ignore("This test starts from base vpp archives");
        }

        var key = fileInfo.Name;
        using var fileStream = fileInfo.OpenRead();
        HashRecursive(fileStream, key, null);
    }

    [Explicit("Reads files from vpp into ram and compares with streamed unpacker")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void CompareUnpackers(FileInfo fileInfo)
    {
        using var fileForRamReading = fileInfo.OpenRead();
        var ramArchive = new VppInMemoryReader().Read(fileForRamReading, fileInfo.Name, CancellationToken.None);

        using var fileForStreaming = fileInfo.OpenRead();
        var streamedArchive = new VppReader().Read(fileForStreaming, fileInfo.Name, CancellationToken.None);
        var archiveInfo = $"{ramArchive.Name} {ramArchive.Mode}";
        Console.WriteLine(archiveInfo);

        var files = ramArchive.LogicalFiles.Zip(streamedArchive.LogicalFiles);
        foreach ((LogicalInMemoryFile ram, LogicalFile streamed) file in files)
        {
            var length = file.streamed.Content is InflaterInputStream
                ? "unsupported"
                : file.streamed.Content.Length.ToString(CultureInfo.InvariantCulture);
            var info = @$"{file.ram.Order} {file.ram.Name}
ram len={file.ram.Content.Length} stream len={length}
{file.streamed.Content.ToString()}";
            try
            {
                //Console.WriteLine($"{file.ram.Order} {file.ram.Name}");
                file.streamed.Name.Should().Be(file.ram.Name, info);
                file.streamed.Offset.Should().Be(file.ram.Offset, info);
                file.streamed.Order.Should().Be(file.ram.Order, info);
                file.streamed.CompressedSize.Should().Be(file.ram.CompressedSize, info);
                file.streamed.NameCString.Value.Should().Equal(file.ram.NameCString.Value, info);

                using var ms = new MemoryStream();
                file.streamed.Content.Position.Should().Be(0, info);
                file.streamed.Content.CopyTo(ms);
                var fromStream = ms.ToArray();
                fromStream.Length.Should().Be(file.ram.Content.Length, info);
                fromStream.Should().Equal(file.ram.Content, info);
            }
            catch (Exception e)
            {
                Console.WriteLine("failed on file:");
                Console.WriteLine(info);
                Console.WriteLine("last state:");
                Console.WriteLine(file.streamed.Content.ToString());
                Console.WriteLine($"pos={file.streamed.Content.Position}");
                Console.WriteLine("======================");
                Console.WriteLine($"{file.ram.Info}");
                Console.WriteLine("======================");
                Console.WriteLine($"{file.streamed.Info}");
                Console.WriteLine("======================");
                Console.WriteLine(e.ToString());
                Assert.Fail();
            }
        }
    }

    private void HashRecursive(Stream stream, string name, string? parentKey)
    {
        var key = parentKey == null
            ? name
            : $"{parentKey}/{name}";
        Console.WriteLine($"hash {key}");

        var hashString = TestUtils.ComputeHash(stream);
        stream.Position = 0;
        if (name.EndsWith(".str2_pc", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".vpp_pc", StringComparison.OrdinalIgnoreCase))
        {
            // this is just for reading flags. actual data is read later
            var vpp = new RfgVppInMemory(new KaitaiStream(stream));
            var alignment = vpp.DetectAlignmentSize(CancellationToken.None);
            var zlibInfo = "none";
            if (vpp.Header.Flags.Mode is RfgVppInMemory.HeaderBlock.Mode.Compressed or RfgVppInMemory.HeaderBlock.Mode.Compacted && vpp.Entries.Any())
            {
                var compressionLevel = vpp.DetectCompressionLevel();
                var zlib = vpp.BlockCompactData?.ZlibHeader ?? vpp.BlockEntryData.Value.First().Value.ZlibHeader;
                zlibInfo = $"{zlib}/{compressionLevel}";
            }

            hashString += $", compressed={vpp.Header.Flags.Compressed}, condensed={vpp.Header.Flags.Condensed}, alignment={alignment}, entries={vpp.Entries.Count}, zlib={zlibInfo}";
        }

        AllHashes[key] = hashString;
        stream.Position = 0;

        if (name.EndsWith(".str2_pc", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".vpp_pc", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"read {key}");
            var entries = new VppInMemoryReader().Read(stream, key, CancellationToken.None).LogicalFiles;
            foreach (var entry in entries)
            {
                using var ms = new MemoryStream(entry.Content);
                HashRecursive(ms, entry.Name, key);
            }
        }
    }

    private static IEnumerable<LogicalInMemoryFile> PatchFiles(IEnumerable<LogicalInMemoryFile> files)
    {
        foreach (var logicalFile in files)
        {
            if (Path.GetExtension(logicalFile.Name).ToLowerInvariant() == ".str2_pc")
            {
                var repackStr2 = RepackStr2(logicalFile);
                yield return logicalFile with { Content = repackStr2 };
            }
            else
            {
                yield return logicalFile;
            }
        }
    }

    private static IEnumerable<LogicalFile> PatchFiles(IEnumerable<LogicalFile> files)
    {
        foreach (var logicalFile in files)
        {
            if (Path.GetExtension(logicalFile.Name).ToLowerInvariant() == ".str2_pc")
            {
                var repackStr2 = RepackStr2(logicalFile);
                yield return logicalFile with { Content = repackStr2 };
            }
            else
            {
                yield return logicalFile;
            }
        }
    }

    private static byte[] RepackStr2(LogicalInMemoryFile logicalInMemoryFile)
    {
        Console.WriteLine($"repacking {logicalInMemoryFile.Name}");
        var ms = new MemoryStream(logicalInMemoryFile.Content);
        var archive = new VppInMemoryReader().Read(ms, logicalInMemoryFile.Name, CancellationToken.None);
        using var dstStream = new MemoryStream();
        var writer = new VppInMemoryWriter(archive);
        writer.WriteAll(dstStream, CancellationToken.None).GetAwaiter().GetResult();
        return dstStream.ToArray();
    }

    private static Stream RepackStr2(LogicalFile logicalFile)
    {
        Console.WriteLine($"repacking {logicalFile.Name}");
        var contentStream = logicalFile.Content;
        var archive = new VppReader().Read(contentStream, logicalFile.Name, CancellationToken.None);
        var dstStream = new MemoryStream();
        var writer = new VppWriter(archive);
        writer.WriteAll(dstStream, CancellationToken.None).GetAwaiter().GetResult();
        dstStream.Seek(0, SeekOrigin.Begin);
        return dstStream;
    }

    public static readonly Dictionary<string, string> AllHashes = new();
}
