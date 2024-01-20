namespace SyncFaction.Toolbox.Models;

public record EntryMetadata(string Name, int Order, ulong Offset, ulong Size, ulong CompressedSize, string Hash);
