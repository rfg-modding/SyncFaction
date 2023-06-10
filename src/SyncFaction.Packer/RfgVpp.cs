using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SyncFaction.Packer;

namespace Kaitai;

public partial class RfgVpp
{
    /// <summary>
    /// Detect alignment size using heuristics to account for badly aligned files
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

    /// <summary>
    /// Decompress solid entries block
    /// </summary>
    /// <param name="token"></param>
    public void ReadCompactedData(CancellationToken token)
    {
        var alignment = DetectAlignmentSize(token);
        token.ThrowIfCancellationRequested();

        // NOTE: dont use BlockCompactData.Value - it reads byte array into memory
        var blockOffset = BlockOffset;
        var rootStream = M_Io.BaseStream;
        var fileLength = rootStream.Length;
        var blockLength = fileLength - blockOffset;
        var compressedStream = new StreamView(rootStream, blockOffset, blockLength);
        var inflaterStream = new InflaterInputStream(compressedStream);
        var decompressedLength = Header.LenData;
        var view = new StreamView(inflaterStream, 0, decompressedLength);
        Header.Flags.OverrideFlagsNone();
        foreach (var entryData in BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            entryData.OverrideAlignmentSize(alignment);
            entryData.OverrideData(new StreamView(view, entryData.XDataOffset, entryData.XLenData));
        }
    }

    /// <summary>
    /// Decompress each entry separately
    /// </summary>
    public void ReadCompressedData(CancellationToken token)
    {
        var offset = BlockOffset;
        foreach (var entryData in BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();

            // TODO maybe get rid of some views here?
            var compressedLength = entryData.DataSize;
            // NOTE: important to calculate it before all overrides
            var totalCompressedLength = entryData.TotalSize;
            var rootStream = M_Io.BaseStream;
            var compressedStream = new StreamView(rootStream, offset, compressedLength);
            var inflaterStream = new InflaterInputStream(compressedStream);
            var decompressedLength = entryData.XLenData;
            var view = new StreamView(inflaterStream, 0, decompressedLength);


            // alignment size is used when creating data, ignoring it
            entryData.OverrideAlignmentSize(0);
            entryData.OverrideDataSize(entryData.XLenData);
            entryData.OverrideData(view);
            offset += totalCompressedLength;
        }
        Header.Flags.OverrideFlagsNone();
    }

    public void FixOffsetOverflow(CancellationToken token)
    {
        long previousValue = 0;
        foreach (var entryData in BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            var longOffset = (long) entryData.XDataOffset;
            if (longOffset < previousValue)
            {
                longOffset += uint.MaxValue;
                longOffset++;
            }

            entryData.LongOffset = longOffset;
            previousValue = longOffset;
        }
    }

    /// <summary>
    /// Read zlib header if entries are compacted or compressed
    /// </summary>
    public int DetectCompressionLevel()
    {
        return Header.Flags.Mode switch
        {
            HeaderBlock.Mode.Compacted => (int)BlockCompactData.ZlibHeader.Flevel,
            HeaderBlock.Mode.Compressed => DetectEntriesCompressionLevel(),
            _ => -1,
        };

        int DetectEntriesCompressionLevel()
        {
            if (Header.NumEntries == 0)
            {
                return -1;
            }

            var level = BlockEntryData.Value.First().ZlibHeader.Flevel;
            foreach (var entryData in BlockEntryData.Value)
            {
                var current = entryData.ZlibHeader.Flevel;
                if (current != level)
                {
                    throw new InvalidOperationException($"Detected different compression ratios between entries. Expected [{level}] but entry {entryData.I} has {current}");
                }
            }

            return (int)level;
        }
    }

    public partial class Zlib
    {
        public override string ToString()
        {
            return $"{HeaderInt:X}/{IsValid}";
        }
    }


    public partial class HeaderBlock
    {
        public partial class HeaderFlags
        {
            public void OverrideFlagsNone()
            {
                _compressed = false;
                _condensed = false;
            }

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
        }

        public enum Mode
        {
            Normal,
            Compressed,
            Condensed,
            Compacted
        }

        public override string ToString()
        {
            return $@"Header:
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
        }
    }

    public partial class EntryData
    {
        public long LongOffset { get; set; }

        /// <summary>
        /// This stream is used for compacted/compressed data and is expected to have only current entry
        /// </summary>
        private Stream? overrideStream;

        public Stream GetDataStream()
        {
            if (overrideStream is not null)
            {
                return overrideStream;
            }

            var length = DataSize;
            var rootStream = M_Root.M_Io.BaseStream;
            var viewStart = M_Root.BlockOffset + LongOffset;
            return new StreamView(rootStream, viewStart, length);
        }

        public void OverrideAlignmentSize(int alignment)
        {
            _padSize = alignment == 0 ? 0 : GetPadSize((int)DataSize, alignment, IsLast);
            f_padSize = true;
        }

        public void OverrideDataSize(uint size)
        {
            _dataSize = size;
            f_dataSize = true;
        }

        public void OverrideData(Stream stream)
        {
            overrideStream = stream;
        }

        public override string ToString()
        {
            return $@"EntryData:
index:       [{I}]
name:        [{XName}]
hash:        [{ToHexString(XNameHash)}]
data length: [{XLenData}]
comp length: [{XLenCompressedData}]
data offset: [{XDataOffset}] (broken if zlib)
long offset: [{LongOffset}] (broken if zlib)
block offst: [{M_Root.BlockOffset}]

data:    [{DataSize}]
pad:     [{PadSize}]
total:   [{TotalSize}]
is last: [{IsLast}]
";
        }
    }
    public partial class Entry
    {
        public override string ToString()
        {
            return $@"Entry:
hash:        [{ToHexString(NameHash)}]
data length: [{LenData}]
comp length: [{LenCompressedData}]
data offset: [{DataOffset}] (may be broken)
";
        }
    }

    public static string ToHexString(byte[] bytes, string separator="") => BitConverter.ToString(bytes).Replace("-", separator);

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

        return (int)(padTo - remainder);
    }
}
