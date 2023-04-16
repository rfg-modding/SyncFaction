using System.Text;

namespace SyncFaction.Packer;

public record LogicalFile(byte[] Content, string Name, int Order)
{
    public Lazy<byte[]> NameCString = new(() => Encoding.ASCII.GetBytes(Name + "\0"));

    public uint CompressedSize { get; set; } = 0xFFFFFFFFu;

    public uint Offset { get; set; }
}