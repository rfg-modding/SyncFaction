using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;

namespace SyncFaction.Packer;

public static class Tools
{
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

    public static LogicalArchive UnpackVpp(Stream source, string name)
    {
        var s = new KaitaiStream(source);
        var vpp = new RfgVpp(s);
        var originalMode = vpp.Header.Flags.Mode;
        return new LogicalArchive(UnpackData(vpp), originalMode, name);
    }

    public static IEnumerable<LogicalFile> UnpackData(RfgVpp vpp)
    {
        if (!vpp.Entries.Any())
        {
            yield break;
        }

        Action? fixupAction = vpp.Header.Flags.Mode switch
        {
            RfgVpp.HeaderBlock.Mode.Compacted => vpp.ReadCompactedData,
            RfgVpp.HeaderBlock.Mode.Compressed => vpp.ReadCompressedData,
            RfgVpp.HeaderBlock.Mode.Condensed => throw new NotImplementedException("Condensed-only mode is not present in vanilla files and is not supported"),
            RfgVpp.HeaderBlock.Mode.Normal => null,
            _ => throw new ArgumentOutOfRangeException()
        };

        try
        {
            fixupAction?.Invoke();
        }
        catch (SharpZipBaseException e)
        {
            var streamSize = "(unknown)";
            if (vpp.M_Io.BaseStream is MemoryStream ms)
            {
                streamSize = ms.Length.ToString();
            }
            var msg = $"Failed to unzip data. Stream size = [{streamSize}]. Header = [{vpp.Header}]";
            throw new InvalidOperationException(msg, e);
        }

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            yield return new LogicalFile(entryData.Value.File, entryData.XName, entryData.I);
            entryData.DisposeAndFreeMemory();
        }
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

    public static async Task CompressZlib(byte[] data, int compressionLevel, Stream destinationStream, CancellationToken token)
    {
        await using var deflater =  new DeflaterOutputStream(destinationStream, new Deflater(compressionLevel)){IsStreamOwner = false};
        await deflater.WriteAsync(data, token);
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

    public static string ToHexString(byte[] bytes, string separator="") => BitConverter.ToString(bytes).Replace("-", separator);

    public static byte[] CircularHash(string input)
    {
        input = input.ToLowerInvariant();

        uint hash = 0;
        for (int i = 0; i < input.Length; i++)
        {
            // rotate left by 6
            hash = (hash << 6) | (hash >> (32 - 6));
            hash = input[i] ^ hash;
        }

        var result = hash;
        return BitConverter.GetBytes(result);
    }
}
