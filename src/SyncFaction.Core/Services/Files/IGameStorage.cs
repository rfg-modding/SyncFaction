using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Models.Files;

namespace SyncFaction.Core.Services.Files;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
public interface IGameStorage : IAppStorage
{
    IDirectoryInfo Bak { get; }

    IDirectoryInfo PatchBak { get; }

    IDirectoryInfo Managed { get; }

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

    IDirectoryInfo GetModDir(IMod mod);

    void InitBakDirectories();
}
