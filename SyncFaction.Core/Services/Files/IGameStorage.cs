using System.Collections.Immutable;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public interface IGameStorage : IAppStorage
{
    IDirectoryInfo Bak { get; }

    IDirectoryInfo PatchBak { get; }

    IDirectoryInfo Managed { get; }

    /// <summary>
    /// List original files
    /// </summary>
    IEnumerable<GameFile> EnumerateStockFiles();

    /// <summary>
    /// List files introduced by patches
    /// </summary>
    IEnumerable<GameFile> EnumeratePatchFiles();

    /// <summary>
    /// List files introduced by mods
    /// </summary>
    IEnumerable<GameFile> EnumerateManagedFiles();

    /// <summary>
    /// Filenames with extensions for all files in game. Relatve paths!
    /// </summary>
    ImmutableSortedDictionary<string, string> VanillaHashes { get; }

    /// <summary>
    /// Filenames without extension for files in /
    /// </summary>
    ImmutableSortedDictionary<string, string> RootFiles { get; }

    /// <summary>
    /// Filenames without extension for files in /data
    /// </summary>
    ImmutableSortedDictionary<string, string> DataFiles { get; }

    IDirectoryInfo GetModDir(IMod mod);

    void InitBakDirectories();

    Task<bool> CheckGameFiles(int threadCount, ILogger log, CancellationToken token);
}
