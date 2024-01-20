using System.Security.Cryptography;
using SyncFaction.Packer.Services.Peg;

namespace SyncFaction.Toolbox;

public static class Utils
{
    public static async Task<string> ComputeHash(FileInfo file)
    {
        await using var s = file.OpenRead();
        return await ComputeHash(s);
    }

    public static async Task<string> ComputeHash(PegFiles pegFiles)
    {
        var cpuHash = await ComputeHash(pegFiles.Cpu);
        var gpuHash = await ComputeHash(pegFiles.Gpu);
        return $"{cpuHash}_{gpuHash}";
    }

    public static async Task<string> ComputeHash(PegStreams pegStreams)
    {
        var cpuHash = await ComputeHash(pegStreams.Cpu);
        var gpuHash = await ComputeHash(pegStreams.Gpu);
        return $"{cpuHash}_{gpuHash}";
    }

    /// <summary>
    /// Expects stream at position 0. Rewinds stream to 0. Does not close stream.
    /// </summary>
    public static async Task<string> ComputeHash(Stream s)
    {
        if (!s.CanSeek)
        {
            throw new ArgumentException($"Need seekable stream, got {s}", nameof(s));
        }

        if (!s.CanRead)
        {
            throw new ArgumentException($"Need readable stream, got {s}", nameof(s));
        }

        if (s.Position != 0)
        {
            throw new ArgumentException($"Expected start of stream, got position = {s.Position}", nameof(s));
        }

        using var sha = SHA256.Create();
        var hashValue = await sha.ComputeHashAsync(s);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        s.Seek(0, SeekOrigin.Begin);
        return hash;
    }


}
