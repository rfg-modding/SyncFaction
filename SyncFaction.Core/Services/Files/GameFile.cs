using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using PleOps.XdeltaSharp.Decoder;

namespace SyncFaction.Core.Services.Files;

public class GameFile
{
    public GameFile(IGameStorage storage, string relativePath, IFileSystem fileSystem)
    {
        var path = Path.Combine(storage.Game.FullName, relativePath);
        FileInfo = fileSystem.FileInfo.FromFileName(path);
        Storage = storage;
    }

    /// <summary>
    /// Map mod files to target files. Mod files can be "rfg.exe", "foo.vpp_pc", "data/foo.vpp_pc", "foo.xdelta", "foo.rfgpatch" and so on
    /// </summary>
    public static GameFile GuessTargetByModFile(IGameStorage storage, IFileInfo modFile)
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

    /// <summary>
    /// Compute hash and compare with expected value. Works only for vanilla files!
    /// </summary>
    public bool IsVanillaByHash()
    {
        var expected = Storage.VanillaHashes[RelativePath.Replace("\\", "/")];
        var hash = ComputeHash();
        return (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsKnown => Storage.VanillaHashes.ContainsKey(RelativePath.Replace("\\", "/"));

    public FileKind Kind
    {
        get
        {
            if (IsKnown)
            {
                return FileKind.Stock;
            }

            if (GetPatchBackupLocation().Exists)
            {
                return FileKind.FromPatch;
            }

            if (GetManagedLocation().Exists)
            {
                return FileKind.FromMod;
            }

            return FileKind.Unmanaged;
        }
    }

    public IFileInfo GetVanillaBackupLocation()
    {
        return FileInfo.FileSystem.FileInfo.FromFileName(Path.Combine(Storage.Bak.FullName, RelativePath));
    }

    public IFileInfo GetPatchBackupLocation()
    {
        return FileInfo.FileSystem.FileInfo.FromFileName(Path.Combine(Storage.PatchBak.FullName, RelativePath));
    }

    public IFileInfo GetManagedLocation()
    {
        return FileInfo.FileSystem.FileInfo.FromFileName(Path.Combine(Storage.Managed.FullName, RelativePath));
    }

    public IFileInfo FindBackup()
    {
        var patch = GetPatchBackupLocation();
        if (patch.Exists)
        {
            return patch;
        }

        return GetVanillaBackupLocation();
    }

    public bool Exists => FileInfo.Exists;

    public bool BackupExists()
    {
        return FindBackup().Exists;
    }

    public IFileInfo? CopyToBackup(bool overwrite, bool isUpdate)
    {
        static FileKind DetermineKind(bool isUpdate, bool isKnown)
        {
            if (isKnown)
            {
                return FileKind.Stock;
            }

            if (isUpdate)
            {
                return FileKind.FromPatch;
            }

            return FileKind.FromMod;
        }

        var kind = DetermineKind(isUpdate, IsKnown);


        if (kind is FileKind.FromMod)
        {
            // new files from mods dont go anywhere, but we keep track in managed directory
            // we just create an empty file to keep track of it. doesn't matter if file in game directory exists
            var managedLocation = GetManagedLocation();
            managedLocation.Create().Close();
            return managedLocation;
        }

        if (!Exists)
        {
            // nothing to back up. this will happen when iterating over updates containing new files
            return null;
        }

        // not new file and not update? it's vanilla file being modified or patched
        IFileInfo GetDestination(FileKind kind)
        {
            switch (kind)
            {
                case FileKind.Stock:
                    // TODO this is total mess:
                    // we are patching or modding vanilla file
                    // if it is not backed up, copy to vanilla bak
                    var vanillaBak = GetVanillaBackupLocation();
                    if (!vanillaBak.Exists)
                    {
                        return vanillaBak;
                    }

                    // if is already backed up and we are updating, work with patch bak
                    if (isUpdate)
                    {
                        return GetPatchBackupLocation();
                    }

                    return vanillaBak;
                case FileKind.FromPatch:
                    // new files from update go to community bak only
                    return GetPatchBackupLocation();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        var dst = GetDestination(kind);
        switch (dst.Exists)
        {
            case true when overwrite:
                // already exists, force copy over, but dont break vanilla backup
                if (dst.FullName == GetVanillaBackupLocation().FullName)
                {
                    throw new InvalidOperationException($"This should not happen: attempt to overwrite vanilla backup of file [{AbsolutePath}]");
                }
                dst.Delete();
                FileInfo.CopyTo(dst.FullName);
                break;
            case true:
                // already exists, do nothing
                break;
            default:
                // doesnt exist. prepare directories recursively then copy file
                Directory.CreateDirectory(dst.Directory.FullName);
                FileInfo.CopyTo(dst.FullName);
                break;
        }

        return dst;
    }

    /// <summary>
    /// Reverts file back to default state
    /// </summary>
    public bool Rollback(bool vanilla)
    {
        switch (Kind)
        {
            case FileKind.Stock:
                var src = vanilla ? GetVanillaBackupLocation() : FindBackup();
                if (!src.Exists)
                {
                    // stock file not present in backups == it was never modified
                    return false;
                }

                src.CopyTo(FileInfo.FullName, true);
                return true;
            case FileKind.FromPatch:
                if (vanilla)
                {
                    Delete();
                    return true;
                }
                var srcPatch = GetPatchBackupLocation();
                if (!srcPatch.Exists)
                {
                    throw new InvalidOperationException($"This should not happen: file is {FileKind.FromPatch} and should've been present in .bak_patch! File: [{AbsolutePath}], Exists: {Exists}");
                }

                srcPatch.CopyTo(FileInfo.FullName, true);
                return true;
            case FileKind.FromMod:
                // forget about this mod file entirely, we don't need it anymore
                var extraModFile = GetManagedLocation();
                extraModFile.Delete();
                if (FileInfo.Exists)
                {
                    Delete();
                }
                return true;
            case FileKind.Unmanaged:
                throw new InvalidOperationException($"This should not happen: file is unmanaged and should've been excluded from rollback! File: [{AbsolutePath}], Exists: {Exists}");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Delete()
    {
        FileInfo.Delete();
    }

    public async Task<bool> ApplyMod(IFileInfo modFile, ILogger log, CancellationToken token)
    {
        return modFile.Extension.ToLowerInvariant() switch
        {
            ".xdelta" => await ApplyXdelta(modFile, log, token),
            ".rfgpatch" or ".txt" or ".jpg" => Skip(modFile, log),  // ignore xml rfgpatch format (unsupported) and common clutter
            var x when x == Ext => ApplyNewFile(modFile, log),
            _ => Skip(modFile, log)
        };
    }

    private IFileInfo FileInfo { get; }

    private IGameStorage Storage { get; }

    private static string GuessRelativePath(IGameStorage storage, IFileInfo modFile)
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
            it is not a known file, so it must be a new file to copy. not an xdelta patch. so extension must be preserved!
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
        log.LogInformation($"+ Skipped unsupported mod file `{modFile.Name}`");
        return true;
    }

    private bool ApplyNewFile(IFileInfo modFile, ILogger log)
    {
        modFile.CopyTo(FileInfo.FullName, true);
        log.LogInformation($"+ Copied `{modFile.Name}`");
        return true;
    }

    private async Task<bool> ApplyXdelta(IFileInfo modFile, ILogger log, CancellationToken cancellationToken)
    {
        var dstFile = FileInfo;
        if (dstFile.Exists)
        {
            dstFile.Delete();
        }
        var srcFile = FindBackup();
        await using var srcStream = srcFile.OpenRead();
        await using var patchStream = modFile.OpenRead();
        await using var dstStream = dstFile.Open(FileMode.Create, FileAccess.ReadWrite);

        try
        {
            using var decoder = new Decoder(srcStream, patchStream, dstStream);
            // TODO log progress
            decoder.ProgressChanged += progress => { cancellationToken.ThrowIfCancellationRequested(); };
            decoder.Run();

            log.LogInformation($"+ **Patched** `{modFile.Name}`");
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, $"XDelta failed: [{srcFile.FullName}] + [{modFile.FullName}] -> [{dstFile.FullName}]");
            throw;
        }
    }
}

public enum FileKind
{
    /// <summary>
    /// File exists in base game distribution
    /// </summary>
    Stock,

    /// <summary>
    /// File is introduced by patch and should be preserved
    /// </summary>
    FromPatch,

    /// <summary>
    /// File is introduced by mod and should be removed
    /// </summary>
    FromMod,

    /// <summary>
    /// File is created by user or game and should be ignored
    /// </summary>
    Unmanaged
}
