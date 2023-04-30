namespace SyncFaction.Packer;

public interface IVppArchiver
{
    Task<LogicalArchiveStreamed> UnpackVpp(Stream source, string name, CancellationToken token);
    Task PackVpp(LogicalArchiveStreamed logicalArchive, Stream destination, CancellationToken token);
}
