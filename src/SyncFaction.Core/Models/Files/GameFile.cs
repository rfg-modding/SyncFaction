using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;

namespace SyncFaction.Core.Services.Files;

public class GameFile
{
    [ExcludeFromCodeCoverage]
    public string Name => FileInfo.FileSystem.Path.GetFileNameWithoutExtension(FileInfo.Name);

    [ExcludeFromCodeCoverage]
    public string Ext => FileInfo.Extension;

    [ExcludeFromCodeCoverage]
    public string NameExt => FileInfo.Name;

    /// <summary>
    /// Directory inside game folder where this file lives: empty string (game root) or "data"
    /// </summary>
    [ExcludeFromCodeCoverage]
    public string RelativeDirectory => FileInfo.FileSystem.Path.GetRelativePath(Storage.Game.FullName, FileInfo.DirectoryName!);

    /// <summary>
    /// "rfg.exe" or "data/foo.vpp_pc"
    /// </summary>
    [ExcludeFromCodeCoverage]
    public string RelativePath => FileInfo.FileSystem.Path.GetRelativePath(Storage.Game.FullName, FileInfo.FullName);

    /// <summary>
    /// Full name of file, useful for reading/writing/moving
    /// </summary>
    [ExcludeFromCodeCoverage]
    public string AbsolutePath => FileInfo.FullName;

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

    public bool Exists => FileInfo.Exists;

    internal IFileInfo FileInfo { get; }

    private IGameStorage Storage { get; }

    public GameFile(IGameStorage storage, string relativePath, IFileSystem fileSystem)
    {
        var path = fileSystem.Path.Combine(storage.Game.FullName, relativePath);
        FileInfo = fileSystem.FileInfo.New(path);
        Storage = storage;
    }

    [ExcludeFromCodeCoverage]
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
    [ExcludeFromCodeCoverage]
    public bool IsVanillaByHash()
    {
        var expected = Storage.VanillaHashes[RelativePath.Replace("\\", "/")];
        var hash = ComputeHash();
        return (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public IFileInfo GetVanillaBackupLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.Bak.FullName, RelativePath));

    public IFileInfo GetPatchBackupLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.PatchBak.FullName, RelativePath));

    public IFileInfo GetManagedLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.Managed.FullName, RelativePath));

    public IFileInfo FindBackup()
    {
        var patch = GetPatchBackupLocation();
        if (patch.Exists)
        {
            return patch;
        }

        return GetVanillaBackupLocation();
    }

    /// <summary>
    /// Get temp file, eg "/data/foo.vpp.tmp". File should not exist!
    /// </summary>
    public IFileInfo GetTmpFile()
    {
        var tmpName = NameExt + ".tmp";
        var fullPath = FileInfo.FileSystem.Path.Join(FileInfo.Directory.FullName, tmpName);
        var fileInfo = FileInfo.FileSystem.FileInfo.New(fullPath);
        if (fileInfo.Exists)
        {
            throw new InvalidOperationException($"Tmp file [{fullPath}] already exists");
        }

        return fileInfo;
    }

    [ExcludeFromCodeCoverage]
    public bool BackupExists() => FindBackup().Exists;

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
            EnsureDirectoriesCreated(managedLocation);
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
                EnsureDirectoriesCreated(dst);
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
        var result = RollbackInternal(vanilla);
        var extraModFile = GetManagedLocation();
        if (extraModFile.Exists)
        {
            // we don't need to track it anymore
            extraModFile.Delete();
        }

        return result;
    }

    public bool RollbackInternal(bool vanilla)
    {
        switch (Kind)
        {
            case FileKind.Stock:
                var src = vanilla
                    ? GetVanillaBackupLocation()
                    : FindBackup();
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

    public void Delete() => FileInfo.Delete();

    private void EnsureDirectoriesCreated(IFileInfo file) => file.FileSystem.Directory.CreateDirectory(file.Directory.FullName);

    /// <summary>
    /// Map mod files to target files. Mod files can be "rfg.exe", "foo.vpp_pc", "data/foo.vpp_pc", "foo.xdelta", "foo.rfgpatch" and so on
    /// </summary>
    public static GameFile GuessTarget(IGameStorage storage, IFileSystemInfo modFile, IDirectoryInfo modDir)
    {
        var relativePath = GuessRelativePath(storage, modFile, modDir);
        return new GameFile(storage, relativePath, modFile.FileSystem);
    }

    private static string GuessRelativePath(IGameStorage storage, IFileSystemInfo modFile, IDirectoryInfo modDir)
    {
        var separator = modFile.FileSystem.Path.DirectorySeparatorChar;
        var relativeModPath = modFile.FileSystem.Path.GetRelativePath(modDir.FullName, modFile.FullName);
        var modNameNoExt = modFile.FileSystem.Path.GetFileNameWithoutExtension(modFile.Name);

        if (!relativeModPath.Contains(separator))
        {
            // it's a file in mod root, probably a replacement or patch for one of known files
            if (storage.RootFiles.TryGetValue(modNameNoExt, out var rootPath))
            {
                return rootPath;
            }

            if (storage.DataFiles.TryGetValue(modNameNoExt, out var dataPath))
            {
                return dataPath;
            }
        }

        var parts = relativeModPath.ToLowerInvariant().Split(separator);
        if (parts.Length == 2 && parts[0] == "data")
        {
            // it's a file in mod/data directory, probably a replacement or patch for one of known data files
            if (storage.DataFiles.TryGetValue(modNameNoExt, out var dataPath))
            {
                return dataPath;
            }
        }

        /*
            it is not a known file, so it must be a new file to copy.

            not an xdelta patch. so extension must be preserved! - TODO change this logic to look for managed files

            mod should mimic game structure: if modFile is inside /data directory in mod structure, it goes to /data
            else it goes to root
            all subdirs are preserved too
        */

        return relativeModPath;
    }
}
