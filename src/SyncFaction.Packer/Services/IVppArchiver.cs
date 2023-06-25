using SyncFaction.Packer.Models;

namespace SyncFaction.Packer.Services;

public interface IVppArchiver
{
    Task<LogicalArchive> UnpackVpp(Stream source, string name, CancellationToken token);
    Task PackVpp(LogicalArchive logicalArchive, Stream destination, CancellationToken token);
}
