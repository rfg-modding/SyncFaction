using System.Collections.Immutable;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Models.Files;

namespace SyncFaction.Core.Services.Files;

public class GameStorage : AppStorage, IGameStorage
{
    public IDirectoryInfo Bak { get; }

    public IDirectoryInfo PatchBak { get; }

    public IDirectoryInfo Managed { get; }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, string> VanillaHashes { get; }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, string> RootFiles { get; }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, string> DataFiles { get; }

    public GameStorage(string gameDir, IFileSystem fileSystem, IDictionary<string, string> fileHashes, ILogger log) : base(gameDir, fileSystem, log)
    {
        Bak = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.BakDirName));
        PatchBak = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.PatchBakDirName));
        Managed = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.ManagedDirName));
        EnsureCreated(Bak);
        EnsureCreated(PatchBak);
        EnsureCreated(Managed);
        VanillaHashes = fileHashes.OrderBy(x => x.Key).ToImmutableSortedDictionary();
        RootFiles = VanillaHashes.Keys.Where(x => x.Split('/').Length == 1).ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x).OrderBy(x => x.Key).ToImmutableSortedDictionary();
        DataFiles = VanillaHashes.Keys.Where(x =>
            {
                var tokens = x.Split('/');
                return tokens.Length == 2 && tokens[0].ToLowerInvariant() == "data";
            })
            .ToDictionary(x => Path.GetFileNameWithoutExtension(x.Split('/').Last()), x => x)
            .OrderBy(x => x.Key)
            .ToImmutableSortedDictionary();
    }

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumerateStockFiles() =>
        VanillaHashes.Keys.Select(x => new GameFile(this, x, fileSystem));

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumeratePatchFiles() =>
        PatchBak.EnumerateFiles("*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(PatchBak.FullName, x.FullName)).Select(x => new GameFile(this, x, fileSystem)).Where(x => x.Kind is FileKind.FromPatch);

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumerateManagedFiles() =>
        Managed.EnumerateFiles("*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(Managed.FullName, x.FullName)).Select(x => new GameFile(this, x, fileSystem));

    public IDirectoryInfo GetModDir(IMod mod) => Game.FileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, mod.IdString));

    public void InitBakDirectories()
    {
        if (!Bak.Exists)
        {
            Bak.Create();
        }

        if (!PatchBak.Exists)
        {
            PatchBak.Create();
        }
    }

    public async Task<bool> CheckGameFiles(int threadCount, ILogger log, CancellationToken token)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        log.LogWarning("Validating game contents. This is one-time thing, but going to take a while");
        // descending name order places bigger files earlier and this gives better check times
        var result = Parallel.ForEachAsync(VanillaHashes.OrderByDescending(x => x.Key),
            new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = threadCount
            },
            async (kv, t) =>
            {
                log.LogInformation("+ *Checking* {key}", kv.Key);
                var file = new GameFile(this, kv.Key, fileSystem);
                if (!await file.IsVanillaByHash(t))
                {
                    log.LogError(@"Action needed:

Found modified game file: {file}

Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.

+ **Steam**: verify integrity of game files and let it download original data
+ **GOG**: reinstall game

Then run SyncFaction again.

*See you later miner!*
", file.RelativePath);
                    cts.Cancel();
                }
            });

        return !cts.IsCancellationRequested;
    }

    private static void EnsureCreated(IDirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            dir.Create();
        }
    }
}
