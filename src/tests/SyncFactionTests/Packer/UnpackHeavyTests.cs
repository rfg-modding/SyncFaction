using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SyncFactionTests.VppRam;

namespace SyncFactionTests.Packer;

[Explicit("Depend on paths tied to steam version")]
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tests")]
public class UnpackHeavyTests
{
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestReadData(FileInfo fileInfo)
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

        var i = 0;
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            entryData.Value.File.Should().HaveCount((int) entryData.DataSize, entryData.ToString());
            entryData.Value.Padding.Should().HaveCount(entryData.PadSize, entryData.ToString());
            (entryData.Value.File.Length + entryData.Value.Padding.Length).Should().Be(entryData.TotalSize, entryData.ToString());
            entryData.DisposeAndFreeMemory();
            i++;
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestDataContents(FileInfo fileInfo)
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

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            if (entryData.XName.EndsWith(".precache", StringComparison.OrdinalIgnoreCase) && entryData.Value.File.Length == 4)
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

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestDataDecompress(FileInfo fileInfo)
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

        if (!vpp.Header.Flags.Compressed)
        {
            Assert.Ignore("This file is not compressed");
        }

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            var decompressAction = () => RfgVppInMemory.DecompressZlib(entryData.Value.File, (int) entryData.XLenData, CancellationToken.None);
            var decompressed = decompressAction.Should().NotThrow(entryData.ToString()).Subject;
            entryData.Value.File.Length.Should().Be((int) entryData.XLenCompressedData);
            decompressed.Length.Should().Be((int) entryData.XLenData);

            entryData.DisposeAndFreeMemory();
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestCompactDataDecompressBlob(FileInfo fileInfo)
    {
        var vpp = RfgVppInMemory.FromFile(fileInfo.FullName);
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

        vpp.ReadCompactedData(CancellationToken.None);
        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            var file = entryData.Value.File;
            var padding = entryData.Value.Padding;

            var fileContent = string.Empty;
            var padContent = string.Empty;
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
            if (file.All(x => x == 0))
            {
                Assert.Warn($"Data contains only zeroes! {entryData}");
            }

            // padding should be empty
            padding.Length.Should().BeLessThan(64, message);
            padding.All(x => x == 0).Should().BeTrue(message);
        }
    }

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllArchiveFiles))]
    public void TestCompactDataDecompressOneByOne(FileInfo fileInfo)
    {
        var vpp = RfgVppInMemory.FromFile(fileInfo.FullName);
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

        var blob = vpp.BlockCompactData.Value;
        using var compressedStream = new MemoryStream(blob);
        using var inputStream = new InflaterInputStream(compressedStream);

        var i = 0;
        uint readingOffset = 0;
        var suppressNoisyWarning = false;

        var alignmentSize = vpp.DetectAlignmentSize(CancellationToken.None);
        foreach (var entry in vpp.Entries)
        {
            var description = $"{i} {entry}";
            var isLast = i == vpp.Entries.Count - 1;

            entry.LenCompressedData.Should().BeGreaterThan(0, description);
            entry.LenCompressedData.Should().BeLessOrEqualTo(entry.LenData, description);

            if (readingOffset < entry.DataOffset)
            {
                // table.vpp_pc is aligned to 64 bytes for some reason
                var delta = entry.DataOffset - readingOffset;
                if (!suppressNoisyWarning)
                {
                    Assert.Warn($"Reading extra {delta} bytes before entry {i} (further warnings suppressed)");
                    suppressNoisyWarning = true;
                }

                var extraPad = ReadBytes(inputStream, (int) delta, CancellationToken.None);
                extraPad.Length.Should().Be((int) delta, description);
                extraPad.All(x => x == 0).Should().BeTrue(description);
                readingOffset += delta;
            }

            var readDataAction = () => ReadBytes(inputStream, (int) entry.LenData, CancellationToken.None);
            var data = readDataAction.Should().NotThrow(description).Subject;
            data.Length.Should().Be((int) entry.LenData, description);
            var padLength = alignmentSize == 0
                ? 0
                : RfgVppInMemory.GetPadSize(data.Length, 16, isLast);
            var readPadAction = () => ReadBytes(inputStream, padLength, CancellationToken.None);
            var pad = readPadAction.Should().NotThrow(description).Subject;
            pad.Length.Should().Be(padLength, description);
            readingOffset.Should().Be(entry.DataOffset, description);
            readingOffset += (uint) data.Length + (uint) pad.Length;
            pad.All(x => x == 0).Should().BeTrue(description);
            i++;
            // unable to check entry.LenCompressedData while reading zlib stream because is read by whole blocks
        }

        readingOffset.Should().Be(vpp.Header.LenData);
    }

    private static byte[] ReadBytes(Stream stream, int count, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        RfgVppInMemory.CopyStream(stream, ms, count, cancellationToken);
        var result = ms.ToArray();
        if (result.Length != count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Was able to read only {result.Length} from stream");
        }

        return result;
    }
}
