using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public record UnpackResult(string RelativePath, ArchiveMetadata ArchiveMetadata, UnpackArgs Args, IReadOnlyList<UnpackArgs> More);