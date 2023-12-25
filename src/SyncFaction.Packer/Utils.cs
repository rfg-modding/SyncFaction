using System.Text;

namespace SyncFaction.Packer;

public static class Utils
{
    public static string ToHexString(byte[] bytes, string separator = "") => BitConverter.ToString(bytes).Replace("-", separator);

    public static void CheckStream(Stream s)
    {
        if (!s.CanSeek)
        {
            throw new ArgumentException($"Need seekable stream, got {s}", nameof(s));
        }

        if (!s.CanWrite)
        {
            throw new ArgumentException($"Need writable stream, got {s}", nameof(s));
        }

        if (s.Position != 0)
        {
            throw new ArgumentException($"Expected start of stream, got position = {s.Position}", nameof(s));
        }

        if (s.Length != 0)
        {
            throw new ArgumentException($"Expected empty stream, got length = {s.Length}", nameof(s));
        }
    }

    public static async Task WriteStream(Stream stream, Stream src, CancellationToken token) => await src.CopyToAsync(stream, token);

    public static async Task Write(Stream stream, byte[] value, CancellationToken token) => await stream.WriteAsync(value, token);

    public static async Task WriteZeroes(Stream stream, int count, CancellationToken token)
    {
        if (count == 0)
        {
            return;
        }

        var value = new byte[count];
        await stream.WriteAsync(value, token);
    }

    public static async Task WriteUint4(Stream stream, long value, CancellationToken token) => await Write(stream, BitConverter.GetBytes((uint) value), token);

    public static async Task WriteUint4(Stream stream, int value, CancellationToken token) => await Write(stream, BitConverter.GetBytes((uint) value), token);

    public static async Task WriteUint2(Stream stream, int value, CancellationToken token) => await Write(stream, BitConverter.GetBytes((ushort) value), token);

    public static Task WriteUint1(Stream stream, int value, CancellationToken _)
    {
        stream.WriteByte((byte) value);
        return Task.CompletedTask;
    }

    public static async Task WriteString(Stream stream, string value, int targetSize, CancellationToken token)
    {
        var chars = Encoding.ASCII.GetBytes(value + "\0");
        var fixedSizeChars = GetArrayOfFixedSize(chars, targetSize);
        // string must always end with \0 character!
        fixedSizeChars[targetSize - 1] = 0;
        await stream.WriteAsync(fixedSizeChars, token);
    }

    public static byte[] GetArrayOfFixedSize(byte[] source, int targetSize)
    {
        var destination = new byte[targetSize];
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
        return destination;
    }
}
