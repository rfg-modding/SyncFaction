using Microsoft.Extensions.Logging;

namespace SyncFactionTests.VppRam;

public class VppInMemoryArchiver
{
    private readonly ILogger<VppInMemoryArchiver> log;

    public VppInMemoryArchiver(ILogger<VppInMemoryArchiver> log)
    {
        this.log = log;
        // TODO use array pool and streamable packer to avoid holding whole thing in memory
        // https://adamsitnik.com/Array-Pool/
        // also, gc in debug does not remove vars in scope, that's why it behaves weirdly
    }

    public async Task<LogicalInMemoryArchive> UnpackVppRam(Stream source, string name, CancellationToken token)
    {
        log.LogDebug("Unpacking vpp: {name}", name);
        var reader = new VppInMemoryReader();
        return await Task.Run(() => reader.Read(source, name, token), token);
    }

    public async Task PackVppRam(LogicalInMemoryArchive logicalInMemoryArchive, Stream destination, CancellationToken token)
    {
        log.LogDebug("Packing vpp: {name}", logicalInMemoryArchive.Name);
        using var writer = new VppInMemoryWriter(logicalInMemoryArchive);
        await writer.WriteAll(destination, token);
    }
}
