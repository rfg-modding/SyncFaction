namespace SyncFaction.Toolbox.Models;

public record ArchiveMetadata(string Name, string Mode, ulong Size, ulong EntryCount, string Hash, MetaEntries MetaEntries);
