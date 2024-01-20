namespace SyncFaction.Packer.Services.Peg;

public record PegStreams(Stream Cpu, Stream Gpu) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Cpu.DisposeAsync();
        await Gpu.DisposeAsync();
    }
}