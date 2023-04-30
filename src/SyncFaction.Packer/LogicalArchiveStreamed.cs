using Kaitai;

namespace SyncFaction.Packer;

public record LogicalArchiveStreamed(IEnumerable<LogicalFileStreamed> LogicalFiles, RfgVppStreamed.HeaderBlock.Mode Mode, string Name);