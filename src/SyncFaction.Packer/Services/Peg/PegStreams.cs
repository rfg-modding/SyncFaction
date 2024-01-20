namespace SyncFaction.Packer.Services.Peg;

public record PegStreams(Stream Cpu, Stream Gpu) : IAsyncDisposable
{
    public string Size => $"{Cpu.Length}_{Gpu.Length}";

    public async ValueTask DisposeAsync()
    {
        await Cpu.DisposeAsync();
        await Gpu.DisposeAsync();
    }
}
