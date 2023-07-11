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
        Bak = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(App.FullName, Constants.BakDirName));
        PatchBak = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(App.FullName, Constants.PatchBakDirName));
        Managed = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(App.FullName, Constants.ManagedDirName));
        EnsureCreated(Bak);
        EnsureCreated(PatchBak);
        EnsureCreated(Managed);
        VanillaHashes = fileHashes.OrderBy(static x => x.Key).ToImmutableSortedDictionary();
        RootFiles = VanillaHashes.Keys.Where(static x => x.Split('/').Length == 1).ToDictionary(x => fileSystem.Path.GetFileNameWithoutExtension(x), static x => x).OrderBy(static x => x.Key).ToImmutableSortedDictionary();
        DataFiles = VanillaHashes.Keys.Where(static x =>
            {
                var tokens = x.Split('/');
                return tokens.Length == 2 && tokens[0].ToLowerInvariant() == "data";
            })
            .ToDictionary(x => fileSystem.Path.GetFileNameWithoutExtension(x.Split('/').Last()), static x => x)
            .OrderBy(static x => x.Key)
            .ToImmutableSortedDictionary();
    }

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumerateStockFiles() =>
        VanillaHashes.Keys.Select(x => new GameFile(this, x, FileSystem));

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumeratePatchFiles() =>
        PatchBak.EnumerateFiles("*", SearchOption.AllDirectories).Select(x => FileSystem.Path.GetRelativePath(PatchBak.FullName, x.FullName)).Select(x => new GameFile(this, x, FileSystem)).Where(static x => x.Kind is FileKind.FromPatch);

    /// <inheritdoc />
    public IEnumerable<GameFile> EnumerateManagedFiles() =>
        Managed.EnumerateFiles("*", SearchOption.AllDirectories).Select(x => FileSystem.Path.GetRelativePath(Managed.FullName, x.FullName)).Select(x => new GameFile(this, x, FileSystem));

    public IDirectoryInfo GetModDir(IMod mod) => Game.FileSystem.DirectoryInfo.New(FileSystem.Path.Combine(App.FullName, mod.IdString));

    public void InitBakDirectories()
    {
        EnsureCreated(Bak);
        EnsureCreated(PatchBak);
    }
}
