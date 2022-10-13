using System.Runtime.CompilerServices;
using SyncFaction.Packer;

namespace Kaitai;

public partial class RfgVpp
{
    /// <summary>
    /// Detect alignment size
    /// </summary>
    public int DetectAlignmentSize()
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

    public void ReadCompactedData()
    {
        var alignment = DetectAlignmentSize();
        var data = Tools.DecompressZlib(BlockCompactData.Value, (int) Header.LenData);
        var stream = new KaitaiStream(data);
        Header.Flags.OverrideFlagsNone();
        var dataBlock = new EntryDataHolder(stream, this, this);
        //dataBlock.M_Io.Seek(0);
        _blockEntryData = dataBlock;
        f_blockEntryData = true;

        foreach (var entryData in BlockEntryData.Value)
        {
            entryData.OverrideAlignmentSize(alignment);
        }
    }

    public void ReadCompressedData()
    {
        foreach (var entryData in BlockEntryData.Value)
        {
            var data = Tools.DecompressZlib(entryData.Value.File, (int)entryData.XLenData);
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

            var level = BlockEntryData.Value.First().Value.ZlibHeader.Flevel;
            foreach (var entryData in BlockEntryData.Value)
            {
                var current = entryData.Value.ZlibHeader.Flevel;
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
            _padSize = alignment == 0 ? 0 : Tools.GetPadSize((int)DataSize, alignment, IsLast);
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

        public override string ToString()
        {
            return $@"EntryData:
index:       [{I}]
name:        [{XName}]
hash:        [{Tools.ToHexString(XNameHash)}]
data length: [{XLenData}]
comp length: [{XLenCompressedData}]
data offset: [{XDataOffset}] (may be broken)

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
hash:        [{Tools.ToHexString(NameHash)}]
data length: [{LenData}]
comp length: [{LenCompressedData}]
data offset: [{DataOffset}] (may be broken)
";
        }
    }

}
