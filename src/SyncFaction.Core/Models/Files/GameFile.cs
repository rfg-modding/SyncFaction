using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Models.Files;

public class GameFile
{
    private readonly ILogger log;

    [ExcludeFromCodeCoverage]
    internal string Name => FileInfo.FileSystem.Path.GetFileNameWithoutExtension(FileInfo.Name);

    [ExcludeFromCodeCoverage]
    public string Ext => FileInfo.Extension;

    [ExcludeFromCodeCoverage]
    private string NameExt => FileInfo.Name;

    /// <summary>
    /// Directory inside game folder where this file lives: empty string (game root) or "data"
    /// </summary>
    [ExcludeFromCodeCoverage]
    public string RelativeDirectory => FileInfo.FileSystem.Path.GetRelativePath(Storage.Game.FullName, FileInfo.DirectoryName!);

    /// <summary>
    /// "rfg.exe" or "data/foo.vpp_pc"
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal string RelativePath => FileInfo.FileSystem.Path.GetRelativePath(Storage.Game.FullName, FileInfo.FullName);

    /// <summary>
    /// Full name of file, useful for reading/writing/moving
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal string AbsolutePath => FileInfo.FullName;

    internal bool IsKnown => Storage.VanillaHashes.ContainsKey(RelativePath.Replace("\\", "/"));

    internal FileKind Kind
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

    internal bool Exists => FileInfo.Exists;

    internal IFileInfo FileInfo { get; }

    internal IGameStorage Storage { get; }

    internal GameFile(IGameStorage storage, string relativePath, IFileSystem fileSystem)
    {
        var path = fileSystem.Path.Combine(storage.Game.FullName, relativePath);
        FileInfo = fileSystem.FileInfo.New(path);
        Storage = storage;
        log = storage.Log; // yeah i know it's stupid but we already store reference to GameStorage here
    }

    internal IFileInfo GetVanillaBackupLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.Bak.FullName, RelativePath));

    internal IFileInfo GetPatchBackupLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.PatchBak.FullName, RelativePath));

    internal IFileInfo GetManagedLocation() => FileInfo.FileSystem.FileInfo.New(FileInfo.FileSystem.Path.Combine(Storage.Managed.FullName, RelativePath));

    internal IFileInfo FindBackup()
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
    internal IFileInfo GetTmpFile()
    {
        var tmpName = NameExt + ".tmp";
        var fullPath = FileInfo.FileSystem.Path.Join(FileInfo.Directory!.FullName, tmpName);
        var fileInfo = FileInfo.FileSystem.FileInfo.New(fullPath);
        if (fileInfo.Exists)
        {
            throw new InvalidOperationException($"Tmp file [{fullPath}] already exists");
        }

        log.LogTrace("Initialized tmp file [{tmp}] for [{file}]", fileInfo.FullName, RelativePath);
        return fileInfo;
    }

    [ExcludeFromCodeCoverage]
    public bool BackupExists() => FindBackup().Exists;

    internal IFileInfo? CopyToBackup(bool overwrite, bool isUpdate)
    {
        FileKind DetermineKind(bool isUpdateFlag, bool isKnown)
        {
            if (isKnown)
            {
                log.LogTrace("Stock: file is known");
                return FileKind.Stock;
            }

            if (isUpdateFlag)
            {
                log.LogTrace("FromPatch: it is update, file is not known");
                return FileKind.FromPatch;
            }

            log.LogTrace("FromMod: not an update, file is not known");
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
            managedLocation.Refresh();
            log.LogTrace("Created managed file [{managed}] for [{file}]", managedLocation, RelativePath);
            return managedLocation;
        }

        if (!Exists)
        {
            // nothing to back up. this will happen when iterating over updates containing new files
            log.LogTrace("Nothing to back up, file does not exist [{file}]", RelativePath);
            return null;
        }

        var dst = GetDestination(kind);
        switch (dst.Exists)
        {
            case true when overwrite:
                // already exists, force copy over, but dont break vanilla backup
                if (dst.FullName == GetVanillaBackupLocation().FullName)
                {
                    throw new InvalidOperationException($"This should not happen: attempt to overwrite vanilla backup of file [{RelativePath}]");
                }

                dst.Delete();
                FileInfo.CopyTo(dst.FullName);
                log.LogTrace("Overwritten existing backup [{backup}] for [{file}]", dst.FullName, RelativePath);
                break;
            case true:
                // already exists, do nothing
                log.LogTrace("Nothing to do, backup exists [{backup}] for [{file}]", dst.FullName, RelativePath);
                break;
            default:
                // doesnt exist. prepare directories recursively then copy file
                EnsureDirectoriesCreated(dst);
                FileInfo.CopyTo(dst.FullName);
                log.LogTrace("Created backup [{backup}] for [{file}]", dst.FullName, RelativePath);
                break;
        }

        return dst;

        // not new file and not update? it's vanilla file being modified or patched
        IFileInfo GetDestination(FileKind fileKind)
        {
            switch (fileKind)
            {
                case FileKind.Stock:
                    // we are patching or modding vanilla file
                    // if it is not backed up, copy to vanilla bak
                    var vanillaBak = GetVanillaBackupLocation();
                    if (!vanillaBak.Exists)
                    {
                        log.LogTrace("Stock: using vanilla backup because it does not exist yet");
                        return vanillaBak;
                    }

                    // if is already backed up and we are updating, work with patch bak
                    if (isUpdate)
                    {
                        log.LogTrace("Stock: it's update, using patch backup because vanilla backup exists");
                        return GetPatchBackupLocation();
                    }

                    log.LogTrace("Stock: it's not update, using vanilla backup");
                    return vanillaBak;
                case FileKind.FromPatch:
                    // new files from update go to patch bak only
                    log.LogTrace("FromPatch: using patch backup");
                    return GetPatchBackupLocation();
                case FileKind.FromMod:
                case FileKind.Unmanaged:
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileKind), fileKind, null);
            }
        }
    }

    /// <summary>
    /// Reverts file back to default state
    /// </summary>
    internal bool Rollback(bool vanilla)
    {
        var result = RollbackInternal(vanilla);
        var extraModFile = GetManagedLocation();
        if (extraModFile.Exists)
        {
            // we don't need to track it anymore
            extraModFile.Delete();
            log.LogTrace("Rollback deleted managed file [{managed}] for [{file}]", extraModFile.FullName, RelativePath);
        }

        log.LogTrace("Rollback result [{result}] for [{file}]", result, RelativePath);
        return result;
    }

    private bool RollbackInternal(bool vanilla)
    {
        switch (Kind)
        {
            case FileKind.Stock:
                var src = vanilla
                    ? GetVanillaBackupLocation()
                    : FindBackup();
                if (!src.Exists)
                {
                    log.LogTrace("RollbackInternal Stock: backup [{src}] does not exist, meaning [{file}] was never modified", src.FullName, RelativePath);
                    return false;
                }

                src.CopyTo(FileInfo.FullName, true);
                log.LogTrace("RollbackInternal Stock: copied from [{src}] to [{file}]", src.FullName, RelativePath);
                return true;
            case FileKind.FromPatch:
                if (vanilla)
                {
                    Delete();
                    log.LogTrace("RollbackInternal FromPatch: deleted [{file}] because rolling back to vanilla", RelativePath);
                    return true;
                }

                var srcPatch = GetPatchBackupLocation();
                if (!srcPatch.Exists)
                {
                    throw new InvalidOperationException($"This should not happen: file is {FileKind.FromPatch} and should've been present in .bak_patch! File: [{RelativePath}], Exists: {Exists}");
                }

                srcPatch.CopyTo(FileInfo.FullName, true);
                log.LogTrace("RollbackInternal FromPatch: copied from [{src}] to [{file}]", srcPatch.FullName, RelativePath);
                return true;
            case FileKind.FromMod:
                if (FileInfo.Exists)
                {
                    Delete();
                    log.LogTrace("RollbackInternal FromMod: deleted [{file}]", RelativePath);
                }
                else
                {
                    log.LogTrace("RollbackInternal FromMod: nothing to do, [{file}] does not exist", RelativePath);
                }

                return true;
            case FileKind.Unmanaged:
                throw new InvalidOperationException($"This should not happen: file is unmanaged and should've been excluded from rollback! File: [{RelativePath}], Exists: {Exists}");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal void Delete() => FileInfo.Delete();

    private static void EnsureDirectoriesCreated(IFileInfo file) => file.FileSystem.Directory.CreateDirectory(file.Directory!.FullName);

    /// <summary>
    /// Map mod files to target files. Mod files can be "rfg.exe", "foo.vpp_pc", "data/foo.vpp_pc", "foo.xdelta", "foo.rfgpatch" and so on
    /// </summary>
    internal static GameFile GuessTarget(IGameStorage storage, IFileSystemInfo modFile, IDirectoryInfo modDir, ILogger log)
    {
        var relativePath = GuessRelativePath(storage, modFile, modDir, log);
        return new GameFile(storage, relativePath, modFile.FileSystem);
    }

    private static string GuessRelativePath(IGameStorage storage, IFileSystemInfo modFile, IDirectoryInfo modDir, ILogger log)
    {
        var separator = modFile.FileSystem.Path.DirectorySeparatorChar;
        var relativeModPath = modFile.FileSystem.Path.GetRelativePath(modDir.FullName, modFile.FullName);
        if (relativeModPath.StartsWith("steam", StringComparison.OrdinalIgnoreCase))
        {
            relativeModPath = relativeModPath[("steam".Length + 1)..];
        }
        else if (relativeModPath.StartsWith("gog", StringComparison.OrdinalIgnoreCase))
        {
            relativeModPath = relativeModPath[("gog".Length + 1)..];
        }

        var modNameNoExt = modFile.FileSystem.Path.GetFileNameWithoutExtension(modFile.Name);

        if (!relativeModPath.Contains(separator))
        {
            // it's a file in mod root, probably a replacement or patch for one of known files
            if (storage.RootFiles.TryGetValue(modNameNoExt, out var rootPath))
            {
                log.LogTrace("Known file in mod root: [{file}]", rootPath);
                return rootPath;
            }

            if (storage.DataFiles.TryGetValue(modNameNoExt, out var dataPath))
            {
                log.LogTrace("Known file in data: [{file}]", dataPath);
                return dataPath;
            }
        }

        var parts = relativeModPath.ToLowerInvariant().Split(separator);
        if (parts.Length == 2 && parts[0] == "data")
        {
            // it's a file in mod/data directory, probably a replacement or patch for one of known data files
            if (storage.DataFiles.TryGetValue(modNameNoExt, out var dataPath))
            {
                log.LogTrace("File in data: [{file}]", dataPath);
                return dataPath;
            }
        }

        var managedFile = storage.FileSystem.FileInfo.New(storage.FileSystem.Path.Combine(storage.Managed.FullName, relativeModPath));
        if (managedFile.Exists)
        {
            log.LogTrace("Managed file: [{file}]", relativeModPath);
            return relativeModPath;
        }

        // if we have "data/new_map.xdelta", try to find "data/new_map.vpp". crash if we have collisions like "data/new_map.vpp" and "data/new_map.etc"
        var managedOtherFile = managedFile.Directory?.Exists == true
            ? managedFile.Directory.EnumerateFiles().SingleOrDefault(x => storage.FileSystem.Path.GetFileNameWithoutExtension(x.Name).Equals(modNameNoExt, StringComparison.OrdinalIgnoreCase))
            : null;
        if (managedOtherFile?.Exists == true)
        {
            var relativeManagedPath = modFile.FileSystem.Path.GetRelativePath(storage.Managed.FullName, managedOtherFile.FullName);
            log.LogTrace("Patch for managed file: [{file}]", relativeManagedPath);
            return relativeManagedPath;
        }

        // not known file, not managed file, so it must be a new file to copy
        log.LogTrace("New file: [{file}]", relativeModPath);
        return relativeModPath;
    }
}
