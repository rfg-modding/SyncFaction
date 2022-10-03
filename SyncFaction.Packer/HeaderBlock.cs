using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

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



    public partial class HeaderBlock
    {
        public partial class HeaderFlags
        {
            public void OverrideFlagsNone()
            {
                _compressed = false;
                _condensed = false;
            }
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
            _padSize = Tools.GetPadSize((int)DataSize, padTo, IsLast);
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

public static class Tools
{
    public static int GetPadSize(int dataSize, int padTo, bool isLast)
    {
        return isLast ? 0 : KaitaiStream.Mod(dataSize, padTo) > 0 ? padTo - KaitaiStream.Mod(dataSize, padTo) : 0;
    }

    public static byte[] ReadBytes(Stream stream, int count)
    {
        using var ms = new MemoryStream();
        CopyStream(stream, ms, count);
        var result = ms.ToArray();
        if (result.Length != count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Was able to read only {result.Length} from stream");
        }

        return result;
    }

    public static byte[] DecompressZlib(byte[] data, int destinationSize)
    {
        var outputStream = new MemoryStream();
        using var compressedStream = new MemoryStream(data);
        using var inputStream = new InflaterInputStream(compressedStream);
        CopyStream(inputStream, outputStream, destinationSize);
        outputStream.Position = 0;
        return outputStream.ToArray();
    }

    private static void CopyStream(Stream input, Stream output, int bytes)
    {
        var buffer = new byte[32768];
        int read;
        while (bytes > 0 && (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
        {
            output.Write(buffer, 0, read);
            bytes -= read;
        }
    }

    public static string HexString(byte[] bytes, string separator="") => BitConverter.ToString(bytes).Replace("-", separator);
}
