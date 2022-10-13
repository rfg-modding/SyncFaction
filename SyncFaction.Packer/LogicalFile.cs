using System.Text;
using Kaitai;

namespace SyncFaction.Packer;

public record LogicalFile(byte[] Content, string Name, int Order)
{
    public Lazy<byte[]> NameCString = new(() => Encoding.ASCII.GetBytes(Name + "\0"));

    public uint CompressedSize { get; set; } = 0xFFFFFFFFu;

    public uint Offset { get; set; }
}

public record LogicalArchive(IEnumerable<LogicalFile> LogicalFiles, RfgVpp.HeaderBlock.Mode Mode, string Name);
