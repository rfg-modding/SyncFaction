using System.Text;

namespace SyncFaction.Packer;

/// <summary>
/// User-friendly representation of vpp entry
/// </summary>
/// <param name="Content">Stream, may be a wrapper around compressed data</param>
/// <param name="Name">Entry name</param>
/// <param name="Order">Entry number from original archive</param>
/// <param name="Info">Debug info about entry properties</param>
/// <param name="CompressedContent">If entry is compressed, this is original stream before decompression. Useful for writing unmodified data without recompression</param>
public record LogicalFile(Stream Content, string Name, int Order, string? Info, Stream? CompressedContent)
{
    public uint CompressedSize { get; set; } = 0xFFFFFFFFu;

    public uint Offset { get; set; }
    public Lazy<byte[]> NameCString = new(() => Encoding.ASCII.GetBytes(Name + "\0"));
}
