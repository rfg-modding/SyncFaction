namespace SyncFaction.Packer;

/// <summary>
/// TODO check for +-1 byte bugs
/// </summary>
public sealed class StreamView : Stream
{
    private readonly Stream stream;
    private readonly long viewLength;
    private readonly long viewStart;

    public StreamView(Stream stream, long viewStart, long viewLength)
    {
        this.stream = stream;
        this.viewLength = viewLength;
        this.viewStart = viewStart;
        Position = 0;
    }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= viewLength)
        {
            return 0;
        }

        // subtracting extra bytes to avoid overflow
        var extraBytes = Position + count >= viewLength
            ? (int) (Position + count - viewLength)
            : 0;

        stream.Seek(viewStart + Position, SeekOrigin.Begin);
        var result = stream.Read(buffer, offset, count-extraBytes);
        if (result > 0)
        {
            Position += result;
        }
        return result;
    }

    public override int ReadByte()
    {
        if (Position+1 >= viewStart+viewLength)
        {
            return -1;
        }

        stream.Seek(viewStart + Position, SeekOrigin.Begin);
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
                if (offset < 0 || offset >= viewLength)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {viewLength}");
                }

                Position = offset;
                return stream.Seek(viewStart + offset, SeekOrigin.Begin);
            case SeekOrigin.Current:
                if (0 < Position + offset || Position + offset >= viewLength)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, position is {Position}, origin is {origin}, max length is {viewLength}");
                }

                Position += offset;
                return stream.Seek(offset, SeekOrigin.Current);
            case SeekOrigin.End:
                if (offset < 0 || offset >= viewLength)
                {
                    throw new InvalidOperationException($"Out of bounds: offset is {offset}, origin is {origin}, max length is {viewLength}");
                }

                Position = viewLength - offset;
                return stream.Seek(viewStart + viewLength - offset, SeekOrigin.Begin);
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }
    }

    public override void SetLength(long value)
    {
        throw new InvalidOperationException($"{nameof(StreamView)} is read-only");
        //viewLength = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException($"{nameof(StreamView)} is read-only");
    }

    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => viewLength;

    public override long Position { get; set; }
}
