using System.Collections;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Force.Crc32;
using Kaitai;
using Microsoft.VisualBasic;

namespace SyncFactionTests;

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

        if (!vpp.Header.IsLarge)
        {
            vpp.Header.LenFileTotal.Should().Be((uint) fileInfo.Length);
        }

        /*

        // read all data and check
        var vppStream = vpp.M_Io;
        var dataSize = vpp.Header.Flags.Compressed ? vpp.Header.LenCompressedData : vpp.Header.LenData;
        var dataBlockOffset = vpp.Header.LenFileTotal - dataSize;
        vppStream.Seek(dataBlockOffset);
        long unpackedOffset = 0;
        foreach (var entryData in vpp.EntryDataBlock.Value)
        {
            entryData.XName.Should().NotBeEmpty();
            CheckHash(entryData);

            var data = vppStream.ReadBytes(entryData.DataSize);
            var pad = vppStream.ReadBytes(entryData.PadSize);
            (data.Length + pad.Length).Should().Be(entryData.TotalSize);

            unpackedOffset += CheckOffset(entryData, unpackedOffset, description);

            if (entryData.IsLast || entryData.PadSize == 0)
            {
                pad.Should().BeEmpty();
            }
            else
            {
                pad.Should().OnlyContain(x => x == 0, description);
            }

            if (vpp.Header.Flags.Compressed && vpp.Header.Flags.Condensed)
            {
                Assert.Fail("TODO more checks for this case");
            }
            else if (vpp.Header.Flags.Compressed)
            {
                // check decompression
                var decompData = Decompress(data);
                ((uint) decompData.Length).Should().Be(entryData.XLenData);
            }
        }*/
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
        RfgVpp.EntryData? prev = null;
        var i = 0;
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            if (prev != null)
            {
                long delta = entryData.XDataOffset - prev.XDataOffset;
                delta.Should().Be(prev.PadSize + prev.DataSize, $"i = {i}");
            }

            prev = entryData;
            i++;
        }
    }
}
