using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace SyncFaction.Core.Services.Files;

public class AppStorage : IAppStorage
{
    public IDirectoryInfo App { get; }

    public IDirectoryInfo Img { get; }

    public IDirectoryInfo Game { get; }

    public IDirectoryInfo Data { get; }

    public IFileSystem FileSystem { get; }

    public ILogger Log { get; }

    public AppStorage(string gameDir, IFileSystem fileSystem, ILogger log)
    {
        this.Log = log;
        FileSystem = fileSystem;
        Game = fileSystem.DirectoryInfo.New(gameDir);
        if (!Game.Exists)
        {
            throw new ArgumentException($"Specified game directory does not exist! [{Game.FullName}]");
        }

        Data = Game.GetDirectories().Single(static x => x.Name == "data");
        App = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(Data.FullName, Constants.AppDirName));
        Img = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(App.FullName, Constants.ImgDirName));
    }

    public bool Init()
    {
        var createdAppDir = EnsureCreated(App);
        EnsureCreated(Img);
        return createdAppDir;
    }

    protected bool EnsureCreated(IDirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            dir.Create();
            Log.LogTrace("Created directory [{dir}]", dir.FullName);
            return true;
        }

        return false;
    }
}
