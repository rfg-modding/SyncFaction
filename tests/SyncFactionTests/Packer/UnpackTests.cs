using FluentAssertions;
using Kaitai;

namespace SyncFactionTests.Packer;

public class UnpackTests
{
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestReadHeader(FileInfo fileInfo)
    {
        var header = RfgVpp.HeaderBlock.FromFile(fileInfo.FullName);
        header.Should().NotBeNull();
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestReadBasics(FileInfo fileInfo)
    {
        var header = RfgVpp.HeaderBlock.FromFile(fileInfo.FullName);
        RfgVpp vpp = null!;
        Action action = () => vpp = RfgVpp.FromFile(fileInfo.FullName);
        action.Should().NotThrow(header.ToString());
        vpp.Header.Should().NotBeNull();
        vpp.Entries.Should().NotBeNull();
        vpp.EntryNames.Should().NotBeNull();
        Console.WriteLine(vpp.Header);

        if (vpp.Header.LenFileTotal != fileInfo.Length)
        {
            Assert.Warn($"Header filesize {vpp.Header.LenFileTotal} does not match actual filesize {fileInfo.Length}. IsLarge={vpp.Header.IsLarge}");
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestReadMetadata(FileInfo fileInfo)
    {
        var vpp  = RfgVpp.FromFile(fileInfo.FullName);

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
                vpp.Entries.Select(x => x.LenCompressedData).Should().OnlyContain(x => x == UInt32.MaxValue);
            }
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestHashNames(FileInfo fileInfo)
    {
        var vpp  = RfgVpp.FromFile(fileInfo.FullName);
        var pairs = vpp.EntryNames.Values
            .Select(x => x.Value)
            .Zip(vpp.Entries.Select(x => x.NameHash));
        foreach (var pair in pairs)
        {
            var hash = TestUtils.CircularHash(pair.First);
            hash.Should().BeEquivalentTo(pair.Second);
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestReadDataOffsets(FileInfo fileInfo)
    {
        var vpp  = RfgVpp.FromFile(fileInfo.FullName);
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
        bool suppressNoisyWarning = false;
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
}
