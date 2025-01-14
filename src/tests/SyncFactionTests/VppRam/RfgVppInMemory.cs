using System.Diagnostics.CodeAnalysis;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;

namespace SyncFactionTests.VppRam;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tests")]
public partial class RfgVppInMemory
{
    /// <summary>
    /// Detect alignment size
    /// </summary>
    public int DetectAlignmentSize(CancellationToken token)
    {
        if (Entries.Count <= 1)
        {
            return 0;
        }

        if (Header.Flags.Mode == HeaderBlock.Mode.Compacted)
        {
            // assume we trust entry.DataOffset for compacted archives
            var readingOffset = 0u;
            var noAlignment = true;
            foreach (var entry in Entries)
            {
                token.ThrowIfCancellationRequested();
                if (readingOffset != entry.DataOffset)
                {
                    noAlignment = false;
                    break;
                }

                readingOffset += entry.LenData;
            }

            if (noAlignment)
            {
                return 0;
            }

            // compacted archive has non-zero alignment, that's ok
        }

        // start with absurdly big value
        var alignment = 8192;
        while (Entries.All(x => x.DataOffset % alignment == 0) == false)
        {
            token.ThrowIfCancellationRequested();
            alignment /= 2;
            if (alignment < 16)
            {
                throw new InvalidOperationException($"Failed to detect alignment size. {alignment} is less than 16");
            }
        }

        if (alignment == 8192)
        {
            throw new InvalidOperationException($"Failed to detect alignment size. {alignment} did not decrease from initial value");
        }

        if (alignment != 16)
        {
            // vanilla table.vpp_pc has alignment = 64
            //throw new InvalidOperationException($"Detected unusual alignment size: {alignment}");
            return alignment;
        }

        return alignment;
    }

    public void ReadCompactedData(CancellationToken token)
    {
        var alignment = DetectAlignmentSize(token);
        token.ThrowIfCancellationRequested();
        var data = DecompressZlib(BlockCompactData.Value, (int) Header.LenData, token);
        var stream = new KaitaiStream(data);
        Header.Flags.OverrideFlagsNone();
        var dataBlock = new EntryDataHolder(stream, this, this);
        //dataBlock.M_Io.Seek(0);
        _blockEntryData = dataBlock;
        f_blockEntryData = true;

        foreach (var entryData in BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            entryData.OverrideAlignmentSize(alignment);
        }
    }

    public void ReadCompressedData(CancellationToken token)
    {
        foreach (var entryData in BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            var data = DecompressZlib(entryData.Value.File, (int) entryData.XLenData, token);
            // alignment size is used when creating data, ignoring it
            entryData.OverrideAlignmentSize(0);
            entryData.OverrideDataSize(entryData.XLenData);
            entryData.OverrideData(data);
        }

        Header.Flags.OverrideFlagsNone();
    }

    /// <summary>
    /// Readz zlib header if entries are compacted or compressed
    /// </summary>
    public int DetectCompressionLevel()
    {
        return Header.Flags.Mode switch
        {
            HeaderBlock.Mode.Compacted => (int) BlockCompactData.ZlibHeader.Flevel,
            HeaderBlock.Mode.Compressed => DetectEntriesCompressionLevel(),
            _ => -1
        };

        int DetectEntriesCompressionLevel()
        {
            if (Header.NumEntries == 0)
            {
                return -1;
            }

            var level = BlockEntryData.Value.First().Value.ZlibHeader.Flevel;
            foreach (var entryData in BlockEntryData.Value)
            {
                var current = entryData.Value.ZlibHeader.Flevel;
                if (current != level)
                {
                    throw new InvalidOperationException($"Detected different compression ratios between entries. Expected [{level}] but entry {entryData.I} has {current}");
                }
            }

            return (int) level;
        }
    }

    public static byte[] DecompressZlib(byte[] data, int destinationSize, CancellationToken token)
    {
        var outputStream = new MemoryStream();
        using var compressedStream = new MemoryStream(data);
        using var inputStream = new InflaterInputStream(compressedStream);
        CopyStream(inputStream, outputStream, destinationSize, token);
        outputStream.Position = 0;
        return outputStream.ToArray();
    }

    public static void CopyStream(Stream input, Stream output, int bytes, CancellationToken token)
    {
        var buffer = new byte[32768];
        int read;
        while (bytes > 0 && (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
        {
            token.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            bytes -= read;
        }
    }

    public static string ToHexString(byte[] bytes, string separator = "") => BitConverter.ToString(bytes).Replace("-", separator);

    public static int GetPadSize(long dataSize, int padTo, bool isLast)
    {
        if (padTo == 0)
        {
            return 0;
        }

        var remainder = dataSize % padTo;
        if (isLast || remainder == 0)
        {
            return 0;
        }

        return (int) (padTo - remainder);
    }

    public partial class Zlib
    {
        public override string ToString() => $"{HeaderInt:X}/{IsValid}";
    }

    public partial class HeaderBlock
    {
        public override string ToString() =>
            $@"Header:
compressed: [{Flags.Compressed}]
condensed:  [{Flags.Condensed}]
shortName:  [{ShortName}]
pathName:   [{PathName}]
entries:    [{NumEntries}]

file size:    [{LenFileTotal}]
entries size: [{LenEntries}]
names size:   [{LenNames}]
data size:    [{LenData}]
comp data sz: [{LenCompressedData}]
";

        public enum Mode
        {
            Normal,
            Compressed,
            Condensed,
            Compacted
        }

        public partial class HeaderFlags
        {
            public Mode Mode
            {
                get
                {
                    if (Compressed && Condensed)
                    {
                        return Mode.Compacted;
                    }

                    if (Compressed && !Condensed)
                    {
                        return Mode.Compressed;
                    }

                    if (!Compressed && Condensed)
                    {
                        return Mode.Condensed;
                    }

                    return Mode.Normal;
                }
            }

            public void OverrideFlagsNone()
            {
                _compressed = false;
                _condensed = false;
            }
        }
    }

    public partial class EntryData
    {
        /// <summary>
        /// Destroys byte value and calls GC for values larger than 10 MiB
        /// </summary>
        public void DisposeAndFreeMemory()
        {
            var size = __raw_value.Length;
            _value = null;
            __raw_value = null;
            if (size > 1 * 1024 * 1024)
            {
                // force free for large chunks of data
                GC.Collect();
            }
        }

        public void OverrideAlignmentSize(int alignment)
        {
            _padSize = alignment == 0
                ? 0
                : GetPadSize((int) DataSize, alignment, IsLast);
            f_padSize = true;
        }

        public void OverrideDataSize(uint size)
        {
            _dataSize = size;
            f_dataSize = true;
        }

        public void OverrideData(byte[] data)
        {
            _value = new EntryContent(new KaitaiStream(data), this);
            f_value = true;
        }

        public override string ToString() =>
            $@"EntryData:
index:       [{I}]
name:        [{XName}]
hash:        [{ToHexString(XNameHash)}]
data length: [{XLenData}]
comp length: [{XLenCompressedData}]
data offset: [{XDataOffset}] (may be broken)
block offst: [{M_Root.BlockOffset}]

data:    [{DataSize}]
pad:     [{PadSize}]
total:   [{TotalSize}]
is last: [{IsLast}]
";
    }

    public partial class Entry
    {
        public override string ToString() =>
            $@"Entry:
hash:        [{ToHexString(NameHash)}]
data length: [{LenData}]
comp length: [{LenCompressedData}]
data offset: [{DataOffset}] (may be broken)
";
    }
}
