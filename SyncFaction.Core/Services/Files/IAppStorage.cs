using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public interface IAppStorage
{
    IDirectoryInfo App { get; }
    IDirectoryInfo Game { get; }
    IDirectoryInfo Data { get; }
    State? LoadState();
    void WriteState(State state);
    string ComputeHash(IFileInfo file);
    bool InitAppDirectory();
    bool CheckFileHashes(bool isGog, ILogger log);
}
