using SyncFaction.Packer;

namespace Kaitai;

public partial class RfgVpp
{
    public void ReadCompactData(int padTo)
    {
        var data = Tools.DecompressZlib(BlockCompactData.Value, (int) Header.LenData);
        var stream = new KaitaiStream(data);
        Header.Flags.OverrideFlagsNone();
        var dataBlock = new EntryDataHolder(stream, this, this);
        //dataBlock.M_Io.Seek(0);
        _blockEntryData = dataBlock;
        f_blockEntryData = true;

        foreach (var entryData in BlockEntryData.Value)
        {
            entryData.OverridePadSize(padTo);
        }
    }

    /// <summary>
    /// Detect pad size. Works only if DataOffsets are valid (use for compacted archives)
    /// </summary>
    public int DetectPadSize()
    {
        var pad = 256;
        while (Entries.All(x => x.DataOffset % pad == 0) == false)
        {
            pad /= 2;
            if (pad < 16)
            {
                throw new InvalidOperationException("Can't detect padding between entries data based on DataOffsets");
            }
        }

        return pad;
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

        public void OverridePadSize(int padTo)
        {
            _padSize = padTo == 0 ? 0 : Tools.GetPadSize((int)DataSize, padTo, IsLast);
            f_padSize = true;
        }

        public override string ToString()
        {
            return $@"EntryData:
index:       [{I}]
name:        [{XName}]
hash:        [{Tools.HexString(XNameHash)}]
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
hash:        [{Tools.HexString(NameHash)}]
data length: [{LenData}]
comp length: [{LenCompressedData}]
data offset: [{DataOffset}] (may be broken)
";
        }
    }

}
