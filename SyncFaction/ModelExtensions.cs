using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.Files;

namespace SyncFaction;

public static class ModelExtensions
{
    public static AppStorage GetAppStorage(this Model model, IFileSystem fileSystem, ILogger log) => new(model.GameDirectory, fileSystem, log);

    public static GameStorage GetGameStorage(this Model model, IFileSystem fileSystem, ILogger log) => new(model.GameDirectory, fileSystem, Hashes.Get(model.IsGog.Value), log);
}
