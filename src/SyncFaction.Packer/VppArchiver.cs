using Microsoft.Extensions.Logging;

namespace SyncFaction.Packer;

public class VppArchiver : IVppArchiver
{
    private readonly ILogger<VppArchiver> log;

    public VppArchiver(ILogger<VppArchiver> log)
    {
        this.log = log;
        // TODO use array pool and streamable packer to avoid holding whole thing in memory
        // https://adamsitnik.com/Array-Pool/
        // also, gc in debug does not remove vars in scope, that's why it behaves weirdly
    }

    public async Task<LogicalArchiveStreamed> UnpackVpp(Stream source, string name, CancellationToken token)
    {
        log.LogDebug("Unpacking vpp: {name}", name);
        var reader = new VppReaderStreamed();
        return await Task.Run(() => reader.Read(source, name, token), token);
    }

    public async Task PackVpp(LogicalArchiveStreamed logicalArchive, Stream destination, CancellationToken token)
    {
        log.LogDebug("Packing vpp: {name}", logicalArchive.Name);
        using var writer = new VppWriterStreamed(logicalArchive);
        await writer.WriteAll(destination, token);
    }

    public async Task<LogicalArchive> UnpackVppRam(Stream source, string name, CancellationToken token)
    {
        log.LogDebug("Unpacking vpp: {name}", name);
        var reader = new VppReader();
        return await Task.Run(() => reader.Read(source, name, token), token);
    }

    public async Task PackVppRam(LogicalArchive logicalArchive, Stream destination, CancellationToken token)
    {
        log.LogDebug("Packing vpp: {name}", logicalArchive.Name);
        using var writer = new VppWriter(logicalArchive);
        await writer.WriteAll(destination, token);
    }
}
