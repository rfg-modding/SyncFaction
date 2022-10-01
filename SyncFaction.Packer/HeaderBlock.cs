namespace Kaitai;

public partial class RfgVpp
{
    public partial class HeaderBlock
    {
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
        public override string ToString()
        {
            return $@"EntryData:
index:       [{I}]
name:        [{XName}]
hash:        [{XNameHash}]
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

    public class ArchiveManager
    {
        public void ReadCompactData()
        {
            throw new NotImplementedException();
        }
    }
}
