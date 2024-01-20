namespace SyncFaction.Toolbox.Models;

public record ArchiveMetadata(string Name, string Mode, string Size, ulong EntryCount, string Hash, MetaEntries MetaEntries);
