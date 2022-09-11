using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using PleOps.XdeltaSharp.Decoder;

namespace SyncFaction.Core.Services.Files;

public class GameFile
{
    public GameFile(IStorage storage, string relativePath, IFileSystem fileSystem)
    {
        var path = Path.Combine(storage.Game.FullName, relativePath);
        FileInfo = fileSystem.FileInfo.FromFileName(path);
        Storage = storage;
    }

    /// <summary>
    /// Map mod files to target files. Mod files can be "rfg.exe", "foo.vpp_pc", "data/foo.vpp_pc", "foo.xdelta", "foo.rfgpatch" and so on
    /// </summary>
    public static GameFile GuessTargetByModFile(IStorage storage, IFileInfo modFile)
    {
        var relativePath = GuessRelativePath(storage, modFile);
        return new GameFile(storage, relativePath, modFile.FileSystem);
    }

    public string Name => Path.GetFileNameWithoutExtension(FileInfo.Name);

    public string Ext => FileInfo.Extension;

    public string NameExt => FileInfo.Name;

    /// <summary>
    /// Directory inside game folder where this file lives: empty string (game root) or "data"
    /// </summary>
    public string RelativeDirectory => Path.GetRelativePath(Storage.Game.FullName, FileInfo.DirectoryName);

    /// <summary>
    /// "rfg.exe" or "data/foo.vpp_pc"
    /// </summary>
    public string RelativePath => Path.GetRelativePath(Storage.Game.FullName, FileInfo.FullName);

    /// <summary>
    /// Full name to file, useful for reading/writing/moving
    /// </summary>
    public string AbsolutePath => FileInfo.FullName;

    public string? ComputeHash()
    {
        if (!Exists)
        {
            return null;
        }

        return Storage.ComputeHash(FileInfo);
    }

    public bool IsVanilla()
    {
        var expected = Storage.VanillaHashes[RelativePath.Replace("\\", "/")];
        var hash = ComputeHash();
        return (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsKnown => Storage.VanillaHashes.ContainsKey(RelativePath.Replace("\\", "/"));

    public IFileInfo GetVanillaBackupLocation()
    {
        return FileInfo.FileSystem.FileInfo.FromFileName(Path.Combine(Storage.Bak.FullName, NameExt));
    }

    public IFileInfo GetCommunityBackupLocation()
    {
        return FileInfo.FileSystem.FileInfo.FromFileName(Path.Combine(Storage.CommunityBak.FullName, NameExt));
    }

    public IFileInfo GetBackupLocation()
    {
        var community = GetCommunityBackupLocation();
        if (community.Exists)
        {
            return community;
        }

        return GetVanillaBackupLocation();
    }

    public bool Exists => FileInfo.Exists;

    public bool BackupExists()
    {
        return GetBackupLocation().Exists;
    }

    public IFileInfo? CopyToBackup(bool overwrite, bool forceToCommunityBak)
    {
        if (!Exists)
        {
            // nothing to back up
            return null;
        }

        var dst = forceToCommunityBak ? GetCommunityBackupLocation() : GetBackupLocation();
        switch (dst.Exists)
        {
            case true when overwrite:
                // already exists, force copy over
                dst.Delete();
                FileInfo.CopyTo(dst.FullName);
                break;
            case true:
                // already exists, do nothing
                break;
            default:
                // doesnt exist
                FileInfo.CopyTo(dst.FullName);
                break;
        }

        return dst;
    }

    public bool RestoreFromBackup()
    {
        var src = GetBackupLocation();
        if (!src.Exists)
        {
            return false;
        }

        src.CopyTo(FileInfo.FullName, true);
        return true;
    }

    public async Task<bool> ApplyMod(IFileInfo modFile, ILogger log, CancellationToken token)
    {
        return modFile.Extension.ToLowerInvariant() switch
        {
            ".xdelta" => await ApplyXdelta(modFile, log, token),
            ".rfgpatch" or ".txt" or ".jpg" => Skip(modFile, log),  // ignore xml-patches (for now) and common clutter
            var x when x == Ext => ApplyNewFile(modFile, log),
            _ => Skip(modFile, log)
        };
    }

    private IFileInfo FileInfo { get; }

    private IStorage Storage { get; }

    private static string GuessRelativePath(IStorage storage, IFileInfo modFile)
    {
        var modNameNoExt = Path.GetFileNameWithoutExtension(modFile.Name);
        if (storage.RootFiles.TryGetValue(modNameNoExt, out var rootPath))
        {
            return rootPath;
        }
        if (storage.DataFiles.TryGetValue(modNameNoExt, out var dataPath))
        {
            return dataPath;
        }

        /*
            it is not a known file, so it must be a new file to copy. not a patch. so extension must be preserved!
            but is it a new file inside / or inside /data?
            let's guess: if modFile is inside /data directory in mod structure, it goes to /data
            else it goes to root
        */

        var lastModDirectory = Path.GetDirectoryName(modFile.FullName).Split(Path.DirectorySeparatorChar).Last();
        if (lastModDirectory.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine("data", modFile.Name);
        }

        return modFile.Name;
    }

    private bool Skip(IFileInfo modFile, ILogger log)
    {
        log.LogInformation($"* Skipped unsupported mod file `{modFile.Name}`");
        return false;
    }

    private bool ApplyNewFile(IFileInfo modFile, ILogger log)
    {
        modFile.CopyTo(FileInfo.FullName, true);
        log.LogInformation($"* Copied `{modFile.Name}`");
        return true;
    }

    private async Task<bool> ApplyXdelta(IFileInfo modFile, ILogger log, CancellationToken cancellationToken)
    {
        var dstFile = FileInfo;
        if (dstFile.Exists)
        {
            dstFile.Delete();
        }
        var srcFile = GetBackupLocation();
        await using var srcStream = srcFile.OpenRead();
        await using var patchStream = modFile.OpenRead();
        await using var dstStream = dstFile.Open(FileMode.Create, FileAccess.ReadWrite);

        using var decoder = new Decoder(srcStream, patchStream, dstStream);
        decoder.Run();

        log.LogInformation($"* **Patched** `{modFile.Name}`");
        return true;
    }
}
