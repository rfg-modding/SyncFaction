using System.Collections.Immutable;
using System.IO.Abstractions;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public interface IStorage
{
    IDirectoryInfo Game { get; }
    IDirectoryInfo Data { get; }
    IDirectoryInfo App { get; }
    IDirectoryInfo Bak { get; }
    IDirectoryInfo CommunityBak { get; }

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
    State? LoadState();
    void WriteState(State state);
    string ComputeHash(IFileInfo file);
}
