namespace SyncFaction.Toolbox.Models;

internal record ArchiveMetadata(string Name, string Mode, ulong Size, ulong EntryCount, string Hash, MetaEntries MetaEntries);
