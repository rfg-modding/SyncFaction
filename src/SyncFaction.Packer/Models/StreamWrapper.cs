using System.Diagnostics.CodeAnalysis;

namespace SyncFaction.Packer.Models;

public sealed class StreamWrapper : Stream
{
    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;

    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Wrapper is not owner of the stream")]
    private readonly Stream stream;

    public StreamWrapper(Stream stream) => this.stream = stream;

    public override async ValueTask DisposeAsync() => await base.DisposeAsync();

    public override void Flush() => stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);
}
