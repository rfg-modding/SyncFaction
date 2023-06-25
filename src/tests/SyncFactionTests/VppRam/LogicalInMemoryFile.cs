using System.Text;

namespace SyncFactionTests.VppRam;

public record LogicalInMemoryFile(byte[] Content, string Name, int Order, string Info)
{
    public uint CompressedSize { get; set; } = 0xFFFFFFFFu;

    public uint Offset { get; set; }
    public Lazy<byte[]> NameCString = new(() => Encoding.ASCII.GetBytes(Name + "\0"));
}
