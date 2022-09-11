using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public class FileManager
{
    private readonly IFileSystem fileSystem;
    private readonly ILogger log;

    public FileManager(IFileSystem fileSystem, ILogger<FileManager> log)
    {
        this.fileSystem = fileSystem;
        this.log = log;
    }

    public bool DoFirstLaunchCheck(IStorage storage)
    {
        var firstLaunch = InitializeDicrectories();
        if (firstLaunch)
        {
            var success = CheckAllFiles();
            return success;
        }

        return true;

        bool InitializeDicrectories()
        {
            var createdAppDir = false;
            if (!storage.App.Exists)
            {
                storage.App.Create();
                createdAppDir = true;
            }

            if (!storage.Bak.Exists)
            {
                storage.Bak.Create();
            }

            if (!storage.CommunityBak.Exists)
            {
                storage.CommunityBak.Create();
            }

            return createdAppDir;
        }

        bool CheckAllFiles()
        {
            log.LogWarning($"Validating game contents. This is one-time thing, but going to take a while");
            foreach (var kv in storage.VanillaHashes.OrderBy(x => x.Key))
            {
                log.LogInformation($"* *Checking* {kv.Key}");
                var file = new GameFile(storage, kv.Key, fileSystem);
                if (!file.IsVanilla())
                {
                    log.LogError(@$"Action needed:

Found modified game file: {file.RelativePath}

Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.

**Use Steam to verify integrity of game files and let it download original data. Then run SyncFaction again.**

*See you later miner!*
");
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Applies mod. Files are reset to community (if present) or vanilla state before installing.
    /// </summary>
    public async Task<bool> InstallModExclusive(IStorage storage, IMod mod, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        var modFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.StartsWith(".mod"));
        var modified = new List<GameFile>();
        foreach (var modFile in modFiles)
        {
            var gameFile = GameFile.GuessTargetByModFile(storage, modFile);
            gameFile.RestoreFromBackup();
            if (gameFile.IsKnown)
            {
                // vanilla file must have backup before we modify it
                gameFile.CopyToBackup(false, false);
            }
            // if file is not known, it's introduced by patch or update, meaning it exists in community bak

            var result = await gameFile.ApplyMod(modFile, log, token);
            if (result)
            {
                modified.Add(gameFile);
            }
        }

        var modApplied = modified.Any();
        if (!modApplied)
        {
            log.LogError("Nothing was changed by mod, maybe it contained only unsupported files");
        }

        return modApplied;
    }

    /// <summary>
    /// Applies community patch. Files are reset to vanilla first. Updates community backup.
    /// </summary>
    public async Task<bool> InstallCommunityPatchBase(IStorage storage, Mod patch, CancellationToken token)
    {
        // won't be needing this anymore
        storage.CommunityBak.Delete(true);
        storage.CommunityBak.Create();
        // some files might have been modified by mod, update or older patch. restore everything from vanilla files
        foreach (var knownFile in storage.VanillaHashes.Keys)
        {
            var gameFile = new GameFile(storage, knownFile, fileSystem);
            gameFile.RestoreFromBackup();
        }
        // TODO: probably delete all files not belonging to the game?

        var patchDir = storage.GetModDir(patch);
        var patchFiles = patchDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.StartsWith(".mod"));
        var modified = new List<GameFile>();
        foreach (var patchFile in patchFiles)
        {
            var gameFile = GameFile.GuessTargetByModFile(storage, patchFile);
            if (gameFile.IsKnown)
            {
                // vanilla file must have backup before we modify it
                // this won't corrupt backup: if file was modded before, it is present in backup already
                gameFile.CopyToBackup(false, false);
            }

            var result = await gameFile.ApplyMod(patchFile, log, token);
            // store patched files as community bak
            gameFile.CopyToBackup(false, true);

            if (result)
            {
                modified.Add(gameFile);
            }
        }

        var modApplied = modified.Any();
        if (!modApplied)
        {
            log.LogError("Nothing was changed by mod, maybe it contained only unsupported files");
            return false;
        }

        return modApplied;
    }

    /// <summary>
    /// Applies community update on top of patch. Files are reset to community (if present) or vanilla state before installing. Updates community backup.
    /// </summary>
    public async Task<bool> InstallCommunityUpdateIncremental(IStorage storage, List<Mod> pendingUpdates, CancellationToken token)
    {
        foreach (var pendingUpdate in pendingUpdates)
        {
            var updateDir = storage.GetModDir(pendingUpdate);
            var modFiles = updateDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.StartsWith(".mod"));
            var modified = new List<GameFile>();
            foreach (var modFile in modFiles)
            {
                var gameFile = GameFile.GuessTargetByModFile(storage, modFile);

                /*
                    some files might have been modified by patch, update, mod, or all of them. restore before applying update:
                        - file not modified = can't restore from anywhere. copy to both places first
                        - file modified by patch = restore from community bak, then copy to community bak
                        - file modified only by mod = restore from bak, then copy to community bak
                        - file modified by patch and mod = restore from community bak, copy to community bak
                    in other words:
                        - if file was modded, it is present in any of backups
                        - restore from community bak
                        - or from bak
                        - if not restored, need to create backup
                */
                gameFile.RestoreFromBackup();
                if (gameFile.IsKnown)
                {
                    // vanilla file must have backup before we modify it
                    // this won't corrupt backup: if file was modified by patch before, it is present in backup already
                    gameFile.CopyToBackup(false, false);
                }

                var result = await gameFile.ApplyMod(modFile, log, token);
                // store patched files as community bak, overwrite existing with new version
                gameFile.CopyToBackup(true, true);
                if (result)
                {
                    modified.Add(gameFile);
                }

                var modApplied = modified.Any();
                if (!modApplied)
                {
                    log.LogError("Nothing was changed by update, maybe it contained only unsupported files");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Restores original files, from vanilla or community backup
    /// </summary>
    public async Task Restore(IStorage storage, bool toVanilla, CancellationToken token)
    {
        log.LogInformation("> Restoring files (to vanilla = {toVanilla})...", toVanilla);
        foreach (var knownFile in storage.VanillaHashes.Keys)
        {
            // TODO: probably delete all files not belonging to the game?
            var gameFile = new GameFile(storage, knownFile, fileSystem);
            var src = GetRestoreLocation(gameFile, toVanilla);
            if (src.Exists)
            {
                src.CopyTo(gameFile.AbsolutePath, true);
                log.LogInformation("* `{file}`: copied from `{src}`", gameFile.RelativePath, src.Directory.Name);
            }
            else
            {
                log.LogInformation("* `{file}`: skipped", gameFile.RelativePath);
            }
        }
        log.LogInformation($"**Success!**");
    }

    private IFileInfo GetRestoreLocation(GameFile gameFile, bool toVanilla)
    {
        var community = gameFile.GetCommunityBackupLocation();
        if (community.Exists && !toVanilla)
        {
            return community;
        }

        return gameFile.GetVanillaBackupLocation();
    }
}
