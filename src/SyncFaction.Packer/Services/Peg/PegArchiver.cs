using Microsoft.Extensions.Logging;
using SyncFaction.Packer.Models.Peg;

namespace SyncFaction.Packer.Services.Peg;

public class PegArchiver : IPegArchiver
{
    private readonly ILogger<VppArchiver> log;

    public PegArchiver(ILogger<VppArchiver> log) => this.log = log;

    public async Task<LogicalTextureArchive> UnpackPeg(Stream cpu, Stream gpu, string name, CancellationToken token)
    {
        log.LogTrace("Unpacking peg [{name}]", name);
        var reader = new PegReader();
        return await Task.Run(() =>
        {
            var peg = reader.Read(cpu, gpu, name, token);
            return peg;
        }, token);
    }

    public async Task PackPeg(LogicalTextureArchive logicalTextureArchive, Stream destinationCpu, Stream destinationGpu, CancellationToken token)
    {
        log.LogTrace("Packing peg [{name}]", logicalTextureArchive.Name);
        using var writer = new PegWriter(logicalTextureArchive);
        await writer.WriteAll(destinationCpu, destinationGpu, token);
    }

    public (FileInfo? cpu, FileInfo? gpu) GetPairFiles(FileInfo input)
    {
        if (!input.Exists)
        {
            return (null, null);
        }

        var ext = input.Extension.ToLowerInvariant();
        var pairExt = ext switch
        {
            ".cpeg_pc" => ".gpeg_pc",
            ".gpeg_pc" => ".cpeg_pc",
            ".cvbm_pc" => ".gvbm_pc",
            ".gvbm_pc" => ".cvbm_pc",
            _ => null
        };
        if (pairExt is null)
        {
            return (null, null);
        }

        var pairPath = Path.ChangeExtension(input.FullName, pairExt);
        var pair = new FileInfo(pairPath);
        if (!pair.Exists)
        {
            return (null, null);
        }

        var first = ext.StartsWith(".c");
        return first ? (input, pair) : (pair, input);
    }
}
