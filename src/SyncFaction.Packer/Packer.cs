using Microsoft.Extensions.Logging;

namespace SyncFaction.Packer;

public class Packer : IPacker
{
    private readonly ILogger<Packer> log;

    public Packer(ILogger<Packer> log)
    {
        this.log = log;
    }

    public async Task<LogicalArchive> UnpackVpp(Stream source, string name, CancellationToken token)
    {
        log.LogDebug("Unpacking vpp: {name}", name);
        var reader = new VppReader();
        return await Task.Run(() => reader.Read(source, name, token), token);
    }

    public async Task PackVpp(LogicalArchive logicalArchive, Stream destination, CancellationToken token)
    {
        log.LogDebug("Packing vpp: {name}", logicalArchive.Name);
        var writer = new VppWriter(logicalArchive);
        await writer.WriteAll(destination, token);
    }
}