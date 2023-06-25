using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace SyncFaction.Packer;

/// <summary>
/// TODO check for +-1 byte bugs
/// </summary>
public sealed class StreamView : Stream
{
    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position { get; set; }

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This class does not own stream")]
    private readonly Stream stream;

    private readonly long viewStart;

    public StreamView(Stream stream, long viewStart, long viewLength)
    {
        this.stream = stream;
        this.Length = viewLength;
        this.viewStart = viewStart;
        Position = 0;
    }

    public override void Flush() => stream.Flush();

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

        if (stream.Position != viewStart + Position)
        {
            if (stream is not InflaterInputStream)
            {
                stream.Seek(viewStart + Position, SeekOrigin.Begin);
            }
        }

        var result = stream.Read(buffer, offset, count - extraBytes);
        if (result > 0)
        {
            Position += result;
        }

        return result;
    }

    public override int ReadByte()
    {
        if (Position + 1 >= viewStart + Length)
        {
            return -1;
        }

        if (stream.Position != viewStart + Position)
        {
            stream.Seek(viewStart + Position, SeekOrigin.Begin);
        }

        var result = stream.ReadByte();
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
                if (offset < 0 || offset >= Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {Length}");
                }

                if (stream is InflaterInputStream)
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
                    var read = stream.Read(buf, 0, delta);
                    pool.Return(buf);
                    return read;
                }

                Position = offset;
                return stream.Seek(viewStart + offset, SeekOrigin.Begin);
            case SeekOrigin.Current:
                if (0 < Position + offset || Position + offset >= Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, position is {Position}, origin is {origin}, max length is {Length}");
                }

                Position += offset;
                return stream.Seek(offset, SeekOrigin.Current);
            case SeekOrigin.End:
                if (offset < 0 || offset >= Length)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {Length}");
                }

                Position = Length - offset;
                return stream.Seek(viewStart + Length - offset, SeekOrigin.Begin);
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }
    }

    public override void SetLength(long value) => throw new InvalidOperationException($"{nameof(StreamView)} is read-only");

    //viewLength = value;
    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException($"{nameof(StreamView)} is read-only");

    public override string ToString()
    {
        var length = stream is InflaterInputStream
            ? "unsupported"
            : stream.Length.ToString(CultureInfo.InvariantCulture);
        return $"stream: len={length} pos={stream.Position}, view: start={viewStart}, len={Length}, pos={Position}";
    }
}
