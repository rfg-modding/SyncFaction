namespace SyncFaction.Toolbox.Models;

internal record EntryMetadata(string Name, int Order, ulong Offset, ulong Size, ulong CompressedSize, string Hash);
