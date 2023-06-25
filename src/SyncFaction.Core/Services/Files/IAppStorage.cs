using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models;

namespace SyncFaction.Core.Services.Files;

public interface IAppStorage
{
    IDirectoryInfo App { get; }
    IDirectoryInfo Img { get; }
    IDirectoryInfo Game { get; }
    IDirectoryInfo Data { get; }
    State? LoadStateFile();
    void WriteStateFile(State state);
    Task<string> ComputeHash(IFileInfo file, CancellationToken token);
    bool Init();
    Task<bool> CheckFileHashes(bool isGog, int threadCount, ILogger log, CancellationToken token);
}
