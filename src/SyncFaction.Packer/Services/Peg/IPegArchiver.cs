using SyncFaction.Packer.Models.Peg;

namespace SyncFaction.Packer.Services.Peg;

public interface IPegArchiver
{
    Task<LogicalTextureArchive> UnpackPeg(Stream cpu, Stream gpu, string name, CancellationToken token);
    Task PackPeg(LogicalTextureArchive logicalTextureArchive, Stream destinationCpu, Stream destinationGpu, CancellationToken token);
    (FileInfo? cpu, FileInfo? gpu) GetPairFiles(FileInfo input);
}