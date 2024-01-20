using System.Security.Cryptography;

namespace SyncFaction.Toolbox;

public static class Utils
{
    public static async Task<string> ComputeHash(FileInfo file)
    {
        await using var s = file.OpenRead();
        return ComputeHash(s);
    }

    public static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashValue = sha.ComputeHash(stream);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }
}
