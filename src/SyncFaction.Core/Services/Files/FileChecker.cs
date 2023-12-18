using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models.Files;

namespace SyncFaction.Core.Services.Files;

public class FileChecker
{
    private readonly ParallelHelper parallelHelper;
    private readonly ILogger<FileChecker> log;

    public FileChecker(ParallelHelper parallelHelper, ILogger<FileChecker> log)
    {
        this.parallelHelper = parallelHelper;
        this.log = log;
    }

    public async Task<bool> CheckGameFiles(IGameStorage gameStorage, int threadCount, CancellationToken token)
    {
        log.LogWarning("Verifying game contents. This is one-time thing, but going to take a while");
        // descending name order places bigger files earlier and this gives better check times
        var data = gameStorage.VanillaHashes
            .OrderByDescending(static x => x.Key, StringComparer.Ordinal)
            .ToList();
        return await parallelHelper.Execute(data, Body, threadCount, TimeSpan.FromSeconds(10), "Verifying", "files", token);

        async Task Body(KeyValuePair<string, string> kv, CancellationTokenSource breaker, CancellationToken t)
        {
            log.LogTrace("Checking [{file}]", kv.Key);
            var file = new GameFile(gameStorage, kv.Key, gameStorage.FileSystem);
            if (!await IsVanillaByHash(file, t))
            {
                log.LogInformation("Found modified game file: `{file}`", file.RelativePath);
                breaker.Cancel();
            }
        }
    }

    public async Task<bool> CheckFileHashes(IAppStorage appStorage, bool isGog, int threadCount, CancellationToken token)
    {
        var files = isGog
            ? Hashes.Gog
            : Hashes.Steam;

        var versionName = isGog
            ? nameof(Hashes.Gog)
            : nameof(Hashes.Steam);
        var data = files
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToList();
        return await parallelHelper.Execute(data, Body, threadCount, TimeSpan.FromSeconds(10), $"Probing {versionName} version", "files", token);

        async Task Body(KeyValuePair<string, string> kv, CancellationTokenSource breaker, CancellationToken t)
        {
            var path = appStorage.FileSystem.Path.Combine(appStorage.Game.FullName, kv.Key);
            var fileInfo = appStorage.FileSystem.FileInfo.New(path);
            var expected = kv.Value;
            var hash = fileInfo.Exists
                ? await ComputeHash(fileInfo, t)
                : null;
            var isVanilla = (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
            if (!isVanilla)
            {
                log.LogTrace("Checking for [{}] version failed: file mismatch `{}`", versionName, fileInfo.Name);
                breaker.Cancel();
            }
        }
    }

    /// <summary>
    /// Compute hash and compare with expected value. Works only for vanilla files!
    /// </summary>
    [ExcludeFromCodeCoverage]
    private async Task<bool> IsVanillaByHash(GameFile gameFile, CancellationToken token)
    {
        var expected = gameFile.Storage.VanillaHashes[gameFile.RelativePath.Replace("\\", "/")];
        var hash = await ComputeHash(gameFile, token);
        return (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    [ExcludeFromCodeCoverage]
    private async Task<string?> ComputeHash(GameFile gameFile, CancellationToken token)
    {
        if (!gameFile.Exists)
        {
            return null;
        }

        return await ComputeHash(gameFile.FileInfo, token);
    }

    internal async Task<string> ComputeHash(IFileInfo file, CancellationToken token)
    {
        using var sha = SHA256.Create();
        await using var fileStream = file.Open(FileMode.Open);
        fileStream.Position = 0;
        var hashValue = await sha.ComputeHashAsync(fileStream, token);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }
}
