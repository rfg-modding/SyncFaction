using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace SyncFaction.Packer.Models;

/// <summary>
/// Limited view of an underlying stream, maintaining its own position
/// </summary>
public sealed class StreamView : Stream
{
    public override bool CanRead => Stream.CanRead;

    public override bool CanSeek => Stream.CanSeek;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position { get; set; }

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This class does not own stream")]
    private Stream Stream { get; }

    private long ViewStart { get; }

    public StreamView(Stream stream, long viewStart, long viewLength)
    {
        Stream = stream;
        Length = viewLength;
        ViewStart = viewStart;
        Position = 0;
    }

    public StreamView ThreadSafeCopy()
    {
        switch (Stream)
        {
            case FileStream fs:
                var newFileStream = File.OpenRead(fs.Name);
                newFileStream.Position = fs.Position;
                var result1 = new StreamView(newFileStream, ViewStart, Length);
                result1.Position = Position;
                return result1;
            case MemoryStream ms:
                var newMemoryStream = new MemoryStream(ms.ToArray());
                newMemoryStream.Position = ms.Position;
                var result2 = new StreamView(newMemoryStream, ViewStart, Length);
                result2.Position = Position;
                return result2;
            case InflaterInputStream iis:
                var inflated = new MemoryStream();
                iis.CopyTo(inflated);
                var result3 = new StreamView(inflated, ViewStart, Length);
                result3.Position = Position;
                return result3;
            case StreamView sv:
                var innerCopy = sv.ThreadSafeCopy().Stream;
                var result4 = new StreamView(innerCopy, ViewStart, Length);
                result4.Position = Position;
                return result4;
            default:
                throw new InvalidOperationException();
        }
    }

    public override void Flush() => Stream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= Length)
        {
            return 0;
        }

        // subtracting extra bytes to avoid overflow
        var extraBytes = Position + count >= Length
            ? (int) (Position + count - Length)
            : 0;

        if (Stream.Position != ViewStart + Position)
        {
            if (Stream is not InflaterInputStream)
            {
                Stream.Seek(ViewStart + Position, SeekOrigin.Begin);
            }
        }

        var result = Stream.Read(buffer, offset, count - extraBytes);
        if (result > 0)
        {
            Position += result;
        }

        return result;
    }

    public override int ReadByte()
    {
        if (Position + 1 >= ViewStart + Length)
        {
            return -1;
        }

        if (Stream.Position != ViewStart + Position)
        {
            Stream.Seek(ViewStart + Position, SeekOrigin.Begin);
        }

        var result = Stream.ReadByte();
        if (result > 0)
        {
            Position += result;
        }

        return result;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                if (offset < 0 || offset > Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {Length}");
                }

                if (Stream is InflaterInputStream)
                {
                    // hack to avoid seeking but still allow fast-forwarding
                    var delta = (int) (offset - Position);
                    if (delta < 0)
                    {
                        throw new InvalidOperationException("Can't seek back to rewind InflaterInputStream");
                    }

                    var pool = ArrayPool<byte>.Shared;
                    var buf = pool.Rent(delta);
                    Position = offset;
                    var read = Stream.Read(buf, 0, delta);
                    pool.Return(buf);
                    return read;
                }

                Position = offset;
                return Stream.Seek(ViewStart + offset, SeekOrigin.Begin);
            case SeekOrigin.Current:
                if (0 < Position + offset || Position + offset > Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, position is {Position}, origin is {origin}, max length is {Length}");
                }

                Position += offset;
                return Stream.Seek(offset, SeekOrigin.Current);
            case SeekOrigin.End:
                if (offset < 0 || offset > Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {Length}");
                }

                Position = Length - offset;
                return Stream.Seek(ViewStart + Length - offset, SeekOrigin.Begin);
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }
    }

    public override void SetLength(long value) => throw new InvalidOperationException($"{nameof(StreamView)} is read-only");

    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException($"{nameof(StreamView)} is read-only");

    public override string ToString()
    {
        var length = Stream is InflaterInputStream
            ? "unsupported (inflater stream)"
            : Stream.Length.ToString(CultureInfo.InvariantCulture);
        return $"StreamView: start={ViewStart}, len={Length}, pos={Position}, stream len={length}, stream pos={Stream.Position}";
    }
}
