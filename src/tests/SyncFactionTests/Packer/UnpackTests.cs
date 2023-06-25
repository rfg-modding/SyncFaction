using FluentAssertions;
using SyncFaction.Packer;
using SyncFactionTests.VppRam;

namespace SyncFactionTests.Packer;

[Explicit("Depend on paths tied to steam version")]
public class UnpackTests
{
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestReadHeader(FileInfo fileInfo)
    {
        var header = RfgVppInMemory.HeaderBlock.FromFile(fileInfo.FullName);
        header.Should().NotBeNull();
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestReadBasics(FileInfo fileInfo)
    {
        var header = RfgVppInMemory.HeaderBlock.FromFile(fileInfo.FullName);
        RfgVppInMemory vppInMemory = null!;
        Action action = () => vppInMemory = RfgVppInMemory.FromFile(fileInfo.FullName);
        action.Should().NotThrow(header.ToString());
        vppInMemory.Header.Should().NotBeNull();
        vppInMemory.Entries.Should().NotBeNull();
        vppInMemory.EntryNames.Should().NotBeNull();
        Console.WriteLine(vppInMemory.Header);

        if (vppInMemory.Header.LenFileTotal != fileInfo.Length)
        {
            Assert.Warn($"Header filesize {vppInMemory.Header.LenFileTotal} does not match actual filesize {fileInfo.Length}. IsLarge={vppInMemory.Header.IsLarge}");
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestReadMetadata(FileInfo fileInfo)
    {
        var vpp = RfgVppInMemory.FromFile(fileInfo.FullName);

        vpp.Entries.Select(x => x.NameOffset).Should().BeInAscendingOrder();

        if (!vpp.Header.IsLarge)
        {
            // large files contain overflown offsets
            vpp.Entries.Select(x => x.DataOffset).ToList().Should().BeInAscendingOrder();
        }

        if (!vpp.Header.Flags.Compressed)
        {
            if (vpp.Entries.Any())
            {
                vpp.Entries.Select(x => x.LenCompressedData).Should().OnlyContain(x => x == uint.MaxValue);
            }
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestHashNames(FileInfo fileInfo)
    {
        var vpp = RfgVppInMemory.FromFile(fileInfo.FullName);
        var pairs = vpp.EntryNames.Values.Select(x => x.Value).Zip(vpp.Entries.Select(x => x.NameHash));
        foreach (var pair in pairs)
        {
            var hash = VppWriter.CircularHash(pair.First);
            hash.Should().BeEquivalentTo(pair.Second);
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestReadDataOffsets(FileInfo fileInfo)
    {
        var vpp = RfgVppInMemory.FromFile(fileInfo.FullName);
        if (vpp.Header.Flags.Compressed && vpp.Header.Flags.Condensed)
        {
            Assert.Ignore("Compact data is tested separately");
        }

        if (!vpp.Entries.Any())
        {
            Assert.Ignore("Empty entries are OK");
        }

        vpp.BlockEntryData.Should().NotBeNull(vpp.Header.ToString());
        vpp.BlockEntryData.Value.Count.Should().Be((int) vpp.Header.NumEntries);

        // check that offsets are valid
        uint readingOffset = 0;
        var suppressNoisyWarning = false;
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            if (entryData.XDataOffset != readingOffset)
            {
                if (!suppressNoisyWarning)
                {
                    Assert.Warn($"Offset for entry {entryData.I} is invalid: expected {readingOffset}, got {entryData.XDataOffset}. delta = {entryData.XDataOffset - readingOffset} (further warnings suppressed)");
                    suppressNoisyWarning = true;
                }
            }

            readingOffset += entryData.DataSize + (uint) entryData.PadSize;
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllFiles))]
    public void TestReadFileSignatures(FileInfo fileInfo)
    {
        var ext = fileInfo.Extension.ToLowerInvariant();
        if (!TestUtils.Signatures.TryGetValue(ext, out var signature))
        {
            Assert.Ignore($"No known signature for {ext}");
        }

        using var s = fileInfo.OpenRead();
        var buffer = new byte[signature.Length];
        s.Read(buffer, 0, buffer.Length);
        buffer.Should().BeEquivalentTo(signature);
    }
}
