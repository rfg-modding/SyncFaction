using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;

namespace SyncFaction.Core.Services.Files;

public interface IModInstaller
{
    Task<bool> ApplyFileMod(GameFile gameFile, IFileInfo modFile, CancellationToken token);
    Task<bool> ApplyVppDirectoryMod(GameFile gameFile, IDirectoryInfo vppDir, CancellationToken token);
    Task<bool> ApplyModInfo(GameFile gameFile, VppOperations vppOperations, CancellationToken token);
}
