namespace SyncFaction.Toolbox.Models;

record EntryMetadata(string Name, int Order, ulong Offset, ulong Size, ulong CompressedSize, string Hash);
