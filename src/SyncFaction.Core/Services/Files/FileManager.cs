using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.ModManager;

namespace SyncFaction.Core.Services.Files;

public record ApplyFileResult(GameFile GameFile, bool Success);
public record ApplyModResult(IReadOnlyList<GameFile> ModifiedFiles, bool Success);

public class FileManager
{
    private readonly ModTools modTools;

    private readonly ILogger log;

    public FileManager(ModTools modTools, ILogger<FileManager> log)
    {
        this.modTools = modTools;
        this.log = log;
    }

    /// <summary>
    /// Applies mod over current game state. <br/>
    /// WARNING: If this is an update, all mods must be reverted and then re-applied
    /// </summary>
    public async Task<ApplyModResult> InstallMod(IGameStorage storage, IMod mod, bool isUpdate, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        var excludeFiles = new HashSet<string>();
        var excludeDirs = new HashSet<string>();
        if (mod.ModInfo is not null)
        {
            foreach (var f in modTools.GetReferencedFiles(mod.ModInfo).Select(x => x.FullName.ToLowerInvariant()))
            {
                excludeFiles.Add(f);
            }

            var modInfoDir = mod.ModInfo.WorkDir.FullName.ToLowerInvariant();
            if (modDir.FullName.ToLowerInvariant() != modInfoDir)
            {
                excludeDirs.Add(modInfoDir);
            }

            var json = JsonConvert.SerializeObject(mod, Formatting.Indented);
            log.LogDebug("Applying mod: {mod}", json);
            // TODO unpack, edit xml and files, etc
            throw new NotImplementedException();
        }

        var modFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(x => !excludeDirs.Contains(x.Directory.FullName.ToLowerInvariant()))
            .Where(x => !excludeFiles.Contains(x.FullName.ToLowerInvariant()))
            .Where(x => x.IsModContent());
        var modified = new List<ApplyFileResult>();
        foreach (var modFile in modFiles)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTargetByModFile(storage, modFile, modDir);
            gameFile.CopyToBackup(false, isUpdate);
            var applyResult = await gameFile.ApplyMod(modFile, log, token);
            var result = new ApplyFileResult(gameFile, applyResult);
            modified.Add(result);
        }

        if (!modified.Any())
        {
            log.LogError("Nothing was changed by mod, maybe it contained only unsupported files");
            return new ApplyModResult(modified.Select(x => x.GameFile).ToList(), false);
        }

        if (modified.Any(x => !x.Success))
        {
            log.LogError("Some mod files failed to apply");
            return new ApplyModResult(modified.Select(x => x.GameFile).ToList(), false);
        }

        if (isUpdate)
        {
            foreach (var result in modified)
            {
                // store patched files as community bak, overwrite existing with new version
                token.ThrowIfCancellationRequested();
                result.GameFile.CopyToBackup(true, true);
            }
        }

        return new ApplyModResult(modified.Select(x => x.GameFile).ToList(), true);
    }

    /// <summary>
    /// Applies community update on top of patch. Files are reset to community (if present) or vanilla state before installing. Updates community backup.
    /// </summary>
    public async Task<ApplyModResult> InstallUpdate(IGameStorage storage, List<IMod> pendingUpdates, bool fromScratch, IEnumerable<IMod> installedMods, CancellationToken token)
    {
        Rollback(storage, fromScratch, token);
        if (fromScratch)
        {
            ForgetUpdates(storage);
        }

        var modifiedFiles = new List<GameFile>();
        foreach (var update in pendingUpdates)
        {
            token.ThrowIfCancellationRequested();
            var result = await InstallMod(storage, update, true, token);
            modifiedFiles.AddRange(result.ModifiedFiles);
            if (!result.Success)
            {
                log.LogError("Install update {id} failed", update.Id);
                return new ApplyModResult(modifiedFiles, false);
            }
        }

        // re-install mods over new updates
        foreach (var mod in installedMods)
        {
            token.ThrowIfCancellationRequested();
            var result = await InstallMod(storage, mod, false, token);
            modifiedFiles.AddRange(result.ModifiedFiles);
            if (!result.Success)
            {
                // TODO probably allow some workarounds to update successfully if mods are failing?
                log.LogError("Re-install mod {id} failed", mod.Id);
                return new ApplyModResult(modifiedFiles, false);
            }
        }

        return new ApplyModResult(modifiedFiles, true);
    }

    /// <summary>
    /// Restores original files, from vanilla or community backup
    /// </summary>
    public void Rollback(IGameStorage storage, bool toVanilla, CancellationToken token)
    {
        var files = storage.EnumerateStockFiles()
            .Concat(storage.EnumeratePatchFiles())
            .Concat(storage.EnumerateManagedFiles());
        foreach (var gameFile in files)
        {
            token.ThrowIfCancellationRequested();
            gameFile.Rollback(toVanilla);
        }

        if (storage.EnumerateManagedFiles().Any())
        {
            throw new InvalidOperationException("Managed files directory should be empty by now");
        }

        // don't automatically nuke patch_bak if restored to vanilla. this allows fast switch between vanilla and updated version
    }

    /// <summary>
    /// When terraform patch is diverged with installed things, nuke them to install from scratch. Otherwise, install only what's new, using patch_bak
    /// </summary>
    internal void ForgetUpdates(IGameStorage storage)
    {
        storage.PatchBak.Delete(true);
        storage.PatchBak.Create();
    }
}
