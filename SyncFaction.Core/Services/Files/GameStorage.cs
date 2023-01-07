using System.Collections.Immutable;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public class GameStorage : AppStorage, IGameStorage
{
    public GameStorage(string gameDir, IFileSystem fileSystem, IDictionary<string, string> fileHashes, ILogger log) : base(gameDir, fileSystem, log)
    {

        Bak = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.BakDirName));
        CommunityBak = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.CommunityBakDirName));
        vanillaHashes = fileHashes.OrderBy(x => x.Key).ToImmutableSortedDictionary();
        rootFiles = VanillaHashes.Keys
            .Where(x => x.Split('/').Length == 1)
            .ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x)
            .OrderBy(x => x.Key)
            .ToImmutableSortedDictionary();
        dataFiles = VanillaHashes.Keys
            .Where(x => x.Split('/').Length == 2)
            .ToDictionary(x => Path.GetFileNameWithoutExtension(x.Split('/').Last()), x => x)
            .OrderBy(x => x.Key)
            .ToImmutableSortedDictionary();
    }

    public IDirectoryInfo Bak { get; }

    public IDirectoryInfo CommunityBak { get; }

    /// <summary>
    /// Filenames with extensions for all files in game. Relatve paths!
    /// </summary>
    public ImmutableSortedDictionary<string, string> VanillaHashes => vanillaHashes;

    /// <summary>
    /// Filenames without extension for files in /
    /// </summary>
    public ImmutableSortedDictionary<string, string> RootFiles => rootFiles;

    /// <summary>
    /// Filenames without extension for files in /data
    /// </summary>
    public ImmutableSortedDictionary<string, string> DataFiles => dataFiles;

    private ImmutableSortedDictionary<string, string> vanillaHashes;
    private ImmutableSortedDictionary<string, string> rootFiles;
    private ImmutableSortedDictionary<string, string> dataFiles;

    public IDirectoryInfo GetModDir(IMod mod)
    {
        return Game.FileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, mod.IdString));
    }

    public void InitBakDirectories()
    {
        if (!Bak.Exists)
        {
            Bak.Create();
        }

        if (!CommunityBak.Exists)
        {
            CommunityBak.Create();
        }
    }

    public Task<bool> CheckGameFiles(int threadCount, ILogger log, CancellationToken token)
    {
        log.LogWarning($"Validating game contents. This is one-time thing, but going to take a while");
        // descending name order places bigger files earlier and this gives better check times
        var result = Parallel.ForEach(VanillaHashes.OrderByDescending(x => x.Key), new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = threadCount
        }, (kv, loopState) =>
        {
            log.LogInformation($"+ *Checking* {kv.Key}");
            var file = new GameFile(this, kv.Key, fileSystem);
            if (!file.IsVanilla())
            {
                log.LogError(@$"Action needed:

Found modified game file: {file.RelativePath}

Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.

+ **Steam**: verify integrity of game files and let it download original data
+ **GOG**: reinstall game

Then run SyncFaction again.

*See you later miner!*
");
                loopState.Stop();
            }
        });

        return Task.FromResult(result.IsCompleted);
    }
}
