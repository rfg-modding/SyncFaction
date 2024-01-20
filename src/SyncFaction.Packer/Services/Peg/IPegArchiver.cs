using SyncFaction.Packer.Models.Peg;

namespace SyncFaction.Packer.Services.Peg;

public interface IPegArchiver
{
    Task<LogicalTextureArchive> UnpackPeg(PegStreams streams, string name, CancellationToken token);
    Task PackPeg(LogicalTextureArchive logicalTextureArchive, PegStreams streams, CancellationToken token);
}
