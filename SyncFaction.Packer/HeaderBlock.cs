using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Kaitai;

public partial class RfgVpp
{
    public void ReadCompactData()
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
            entryData.OverridePadSize();
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

        public void OverridePadSize()
        {
            var padTo = 16;
            _padSize = (int) (IsLast ? 0 : KaitaiStream.Mod(DataSize, padTo) > 0 ? padTo - KaitaiStream.Mod(DataSize, padTo) : 0);
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

    public static class Tools
    {
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
}
