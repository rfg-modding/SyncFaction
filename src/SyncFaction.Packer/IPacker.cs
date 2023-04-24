namespace SyncFaction.Packer;

public interface IPacker
{
    Task<LogicalArchive> UnpackVpp(Stream source, string name, CancellationToken token);
    Task PackVpp(LogicalArchive logicalArchive, Stream destination, CancellationToken token);
}
