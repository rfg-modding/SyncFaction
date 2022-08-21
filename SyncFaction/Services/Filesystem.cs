using System.IO;
using System.Linq;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction.Services;

public class Filesystem
{
    public Filesystem(string gameDir)
    {
        Game = new DirectoryInfo(gameDir);
        Data = Game.GetDirectories().Single(x => x.Name == "data");
        App = new DirectoryInfo(Path.Combine(Data.FullName, Constants.AppDirName));
        Bak = new DirectoryInfo(Path.Combine(App.FullName, Constants.BakDirName));

    }

    public DirectoryInfo Game { get; }
    public DirectoryInfo Data { get; }
    public DirectoryInfo App { get; }
    public DirectoryInfo Bak { get; }

    public DirectoryInfo ModDir(IMod mod)
    {
        return new DirectoryInfo(Path.Combine(App.FullName, mod.IdString));
    }

    public long? GetInstalledCommunityPatchId()
    {
        var file = App.EnumerateFiles().SingleOrDefault(x => x.Name == Constants.CommunityPatchIdFile);
        if (file == null)
        {
            return null;
        }

        var content = File.ReadAllText(file.FullName).Trim();
        return long.Parse(content);
    }
    
    public void WriteCommunityPatchId(long id)
    {
        var file = new FileInfo(Path.Combine(App.FullName, Constants.CommunityPatchIdFile));
        if (file.Exists)
        {
            file.Delete();
        }

        File.WriteAllText(file.FullName, id.ToString());
    }
}
