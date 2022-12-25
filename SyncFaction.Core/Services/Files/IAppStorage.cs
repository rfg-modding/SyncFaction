using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public interface IAppStorage
{
    IDirectoryInfo App { get; }
    IDirectoryInfo Img { get; }
    IDirectoryInfo Game { get; }
    IDirectoryInfo Data { get; }
    State? LoadStateFile();
    void WriteStateFile(State state);
    string ComputeHash(IFileInfo file);
    bool Init();
    bool CheckFileHashes(bool isGog, int threadCount, ILogger log, CancellationToken token);
}
