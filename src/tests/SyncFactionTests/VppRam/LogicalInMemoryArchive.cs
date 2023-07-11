namespace SyncFactionTests.VppRam;

public record LogicalInMemoryArchive(IEnumerable<LogicalInMemoryFile> LogicalFiles, RfgVppInMemory.HeaderBlock.Mode Mode, string Name);
