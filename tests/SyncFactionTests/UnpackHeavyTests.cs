using System.Text;
using FluentAssertions;
using Kaitai;

namespace SyncFactionTests;

public class UnpackHeavyTests
{
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestReadData(FileInfo fileInfo)
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

        var i = 0;
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            entryData.Value.File.Should().HaveCount((int) entryData.DataSize, entryData.ToString());
            entryData.Value.Padding.Should().HaveCount((int) entryData.PadSize, entryData.ToString());
            (entryData.Value.File.Length + entryData.Value.Padding.Length).Should().Be(entryData.TotalSize, entryData.ToString());
            entryData.DisposeAndFreeMemory();
            i++;
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestDataContents(FileInfo fileInfo)
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

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            if (entryData.XName.EndsWith(".precache") && entryData.Value.File.Length == 4)
            {
                // small precache files are always empty
                entryData.Value.File.All(x => x == 0).Should().BeTrue(entryData.ToString());
            }
            else
            {
                // data chunks are expected to have something useful
                entryData.Value.File.All(x => x == 0).Should().BeFalse(entryData.ToString());
            }

            if (fileInfo.Name != "steam.vpp_pc")
            {
                // steam vpp has garbage between entries
                entryData.Value.Padding.All(x => x == 0).Should().BeTrue(entryData.ToString());
            }

            entryData.DisposeAndFreeMemory();
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestDataDecompress(FileInfo fileInfo)
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

        if (!vpp.Header.Flags.Compressed)
        {
            Assert.Ignore("This file is not compressed");
        }

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            Func<byte[]> decompressAction = () => RfgVpp.Tools.DecompressZlib(entryData.Value.File, (int)entryData.XLenData);
            var decompressed = decompressAction.Should().NotThrow(entryData.ToString()).Subject;
            entryData.Value.File.Length.Should().Be((int)entryData.XLenCompressedData);
            decompressed.Length.Should().Be((int)entryData.XLenData);

            entryData.DisposeAndFreeMemory();
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void TestCompactDataDecompress(FileInfo fileInfo)
    {
        var vpp  = RfgVpp.FromFile(fileInfo.FullName);
        if (!vpp.Header.Flags.Compressed || !vpp.Header.Flags.Condensed)
        {
            Assert.Ignore("This file contains normal data");
        }
        if (!vpp.Entries.Any())
        {
            Assert.Ignore("Empty entries are OK");
        }

        vpp.BlockEntryData.Should().BeNull();
        vpp.BlockCompactData.Should().NotBeNull();

        vpp.ReadCompactData();
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            var file = entryData.Value.File;
            var padding = entryData.Value.Padding;

            string fileContent = string.Empty;
            string padContent = string.Empty;
            try
            {
                fileContent = Encoding.ASCII.GetString(file);
            }
            catch (Exception)
            {
                fileContent = "not a string";
            }
            try
            {
                padContent = Encoding.ASCII.GetString(padding);
            }
            catch (Exception)
            {
                padContent = "not a string";
            }

            var message = $"{entryData}\n=== file ===\n{fileContent}\n=== padding ===\n{padContent}";

            // data chunks are expected to have something useful
            file.All(x => x == 0).Should().BeFalse(message);
            // padding should be empty
            padding.Length.Should().BeLessThan(64, message);
            padding.All(x => x == 0).Should().BeTrue(message);

        }
    }
}
