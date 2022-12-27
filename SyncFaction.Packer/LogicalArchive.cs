using Kaitai;

namespace SyncFaction.Packer;

public record LogicalArchive(IEnumerable<LogicalFile> LogicalFiles, RfgVpp.HeaderBlock.Mode Mode, string Name);