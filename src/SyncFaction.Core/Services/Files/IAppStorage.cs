using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace SyncFaction.Core.Services.Files;

public interface IAppStorage
{
    IDirectoryInfo App { get; }
    IDirectoryInfo Img { get; }
    IDirectoryInfo Game { get; }
    IDirectoryInfo Data { get; }
    IFileSystem FileSystem { get; }
    bool Init();
    ILogger Log { get; }
}
