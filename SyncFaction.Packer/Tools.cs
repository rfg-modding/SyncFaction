using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;

namespace SyncFaction.Packer;

public static class Tools
{
    public static IEnumerable<LogicalFile> UnpackVpp(Stream source, string name)
    {
        var s = new KaitaiStream(source);
        var vpp = new RfgVpp(s);
        if (vpp.Header.Flags.Mode == RfgVpp.HeaderBlock.Mode.Compacted)
        {
            var pad = vpp.DetectPadSize();
            vpp.ReadCompactData(pad);
        }

        if (!vpp.Entries.Any())
        {
            yield break;
        }

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            yield return new LogicalFile()
            {
                Content = entryData.Value.File,
                Order = entryData.I,
                Name = entryData.XName,
                ParentName = name
            };
            entryData.DisposeAndFreeMemory();
        }
    }

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
