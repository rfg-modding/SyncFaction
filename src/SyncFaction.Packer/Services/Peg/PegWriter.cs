using SyncFaction.Packer.Models.Peg;

namespace SyncFaction.Packer.Services.Peg;

public class PegWriter : IDisposable
{
    private readonly byte[] HeaderMagic =
    {
        71, 69, 75, 86
    };

    private readonly byte[] HeaderVersion =
    {
        0x0A, 0
    };

    private readonly LogicalTextureArchive logicalTextureArchive;
    private readonly List<LogicalTexture> logicalTextures;

    public PegWriter(LogicalTextureArchive logicalTextureArchive)
    {
        this.logicalTextureArchive = logicalTextureArchive;
        logicalTextures = logicalTextureArchive.LogicalTextures.ToList();
    }

    public async Task WriteAll(Stream destinationCpu, Stream destinationGpu, CancellationToken token)
    {
        Utils.CheckStream(destinationCpu);
        Utils.CheckStream(destinationGpu);
        CheckEntries(token);
        var entryNamesBlock = await GetEntryNamesUpdateOffsets(token);
        var headerBlockSize = 24;
        // occupy space for later overwriting
        await Utils.WriteZeroes(destinationCpu, headerBlockSize, token);
        foreach (var logicalTexture in logicalTextures)
        {
            await WriteEntry(logicalTexture, destinationCpu, destinationGpu, token);
        }
        await Utils.Write(destinationCpu, entryNamesBlock, token);

        var header = await GetHeader(destinationCpu.Position, destinationGpu.Position, token);
        destinationCpu.Position = 0;
        await Utils.Write(destinationCpu, header, token);
    }

    private async Task<byte[]> GetHeader(long cpuSize, long gpuSize, CancellationToken token)
    {
        await using var ms = new MemoryStream();
        await Utils.Write(ms, HeaderMagic, token);
        await Utils.Write(ms, HeaderVersion, token);
        await Utils.WriteUint2(ms, 0, token);
        await Utils.WriteUint4(ms, cpuSize, token);
        await Utils.WriteUint4(ms, gpuSize, token);
        await Utils.WriteUint2(ms, logicalTextureArchive.LogicalTextures.Count, token);
        await Utils.WriteUint2(ms, 0, token);
        await Utils.WriteUint2(ms, logicalTextureArchive.LogicalTextures.Count, token);
        await Utils.WriteUint2(ms, logicalTextureArchive.Align, token);
        return ms.ToArray();
    }

    private async Task WriteEntry(LogicalTexture logicalTexture, Stream destinationCpu, Stream destinationGpu, CancellationToken token)
    {
        await Utils.WriteUint4(destinationCpu, destinationGpu.Position, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.Size.Width, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.Size.Height, token);
        await Utils.WriteUint2(destinationCpu, (int)logicalTexture.Format, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.Source.Width, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.AnimTiles.Width, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.AnimTiles.Height, token);
        await Utils.WriteUint2(destinationCpu, 1, token);
        await Utils.WriteUint2(destinationCpu, (int)logicalTexture.Flags, token);
        await Utils.WriteUint4(destinationCpu, logicalTexture.NameOffset, token);
        await Utils.WriteUint2(destinationCpu, logicalTexture.Source.Height, token);
        await Utils.WriteUint1(destinationCpu, 1, token);
        await Utils.WriteUint1(destinationCpu, logicalTexture.MipLevels, token);
        await Utils.WriteUint4(destinationCpu, logicalTexture.Data.Length, token);
        await Utils.WriteUint4(destinationCpu, 0, token);
        await Utils.WriteUint4(destinationCpu, 0, token);
        await Utils.WriteUint4(destinationCpu, 0, token);
        await Utils.WriteUint4(destinationCpu, 0, token);

        // finally write data to gpu file
        await Utils.WriteStream(destinationGpu, logicalTexture.Data, token);
        await Utils.WriteZeroes(destinationGpu, logicalTexture.PadSize, token);
    }

    private async Task<byte[]> GetEntryNamesUpdateOffsets(CancellationToken token)
    {
        if (logicalTextures.Count == 0)
        {
            return Array.Empty<byte>();
        }

        await using var ms = new MemoryStream();
        foreach (var logicalTexture in logicalTextures)
        {
            token.ThrowIfCancellationRequested();
            logicalTexture.NameOffset = (int)ms.Position;
            await Utils.Write(ms, logicalTexture.GetNameCString(), token);
        }

        return ms.ToArray();
    }


    private void CheckEntries(CancellationToken token)
    {
        var i = 0;
        foreach (var logicalTexture in logicalTextures)
        {
            token.ThrowIfCancellationRequested();
            if (logicalTexture.Order != i)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalTexture), logicalTexture.Order, $"Invalid order, expected {i}");
            }

            if (string.IsNullOrWhiteSpace(logicalTexture.Name))
            {
                throw new ArgumentOutOfRangeException(nameof(logicalTexture), logicalTexture.Name, "Invalid name, expected meaningful string");
            }

            if (logicalTexture.Align != logicalTextureArchive.Align)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalTexture), logicalTexture.Align, "Invalid align, expected same as in archive header");
            }

            i++;
        }
    }

    public void Dispose() =>
        // GC magic!
        logicalTextures.Clear();
}
