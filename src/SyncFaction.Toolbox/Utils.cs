using System.Security.Cryptography;
using SyncFaction.Packer.Services.Peg;

namespace SyncFaction.Toolbox;

public static class Utils
{
    public static async Task<string> ComputeHash(FileInfo file)
    {
        await using var s = file.OpenRead();
        return ComputeHash(s);
    }

    public static async Task<string> ComputeHash(PegFiles pegFiles)
    {
        var cpuHash = await ComputeHash(pegFiles.Cpu);
        var gpuHash = await ComputeHash(pegFiles.Gpu);
        return $"{cpuHash}_{gpuHash}";
    }

    public static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashValue = sha.ComputeHash(stream);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }
}
