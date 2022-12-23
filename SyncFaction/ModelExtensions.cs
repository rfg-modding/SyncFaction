using System.IO.Abstractions;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.Files;

namespace SyncFaction;

public static class ModelExtensions
{
    public static AppStorage GetAppStorage(this Model model, IFileSystem fileSystem) => new AppStorage(model.GameDirectory, fileSystem);

    public static GameStorage GetGameStorage(this Model model, IFileSystem fileSystem) => new GameStorage(model.GameDirectory, fileSystem, Hashes.Get(model.IsGog));
}