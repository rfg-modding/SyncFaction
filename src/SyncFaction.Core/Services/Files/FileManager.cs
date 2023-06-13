using System.Collections.Immutable;
using System.Runtime;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.ModManager;
using SyncFaction.ModManager.Models;

namespace SyncFaction.Core.Services.Files;

public class FileManager
{
    private readonly ModTools modTools;
    private readonly IModInstaller modInstaller;

    private readonly ILogger log;

    public FileManager(ModTools modTools, IModInstaller modInstaller, ILogger<FileManager> log)
    {
        this.modTools = modTools;
        this.modInstaller = modInstaller;
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
        var modified = new List<ApplyFileResult>();

        if (mod.ModInfo is not null)
        {
            var operations = modTools.BuildOperations(mod.ModInfo);

            excludeFiles.Add(modDir.EnumerateFiles("modinfo.xml", SearchOption.AllDirectories).First().FullName.ToLowerInvariant());
            foreach (var op in operations.FileSwaps)
            {
                // NOTE: this won't exclude files mentioned in selectbox inputs!
                // to avoid clutter, instruct users to place modinfo.xml and all relative stuff into a subfolder
                excludeFiles.Add(op.Target.FullName.ToLowerInvariant());
            }

            // exclude dir with modinfo.xml, recursively
            // if it's mod root, ignore it anyway, to avoid clutter when using old mods
            var modInfoDir = mod.ModInfo.WorkDir.FullName.ToLowerInvariant();
            excludeDirs.Add(modInfoDir);

            //var json = JsonConvert.SerializeObject(mod, Formatting.Indented);
            //log.LogDebug("Applying mod: {mod}", json);

            foreach (var vppOps in operations.VppOperations)
            {
                var dataVpp = vppOps.Key;
                var fakeVppFile = modDir.FileSystem.FileInfo.New(modDir.FileSystem.Path.Combine(modDir.FullName, dataVpp));
                var gameFile = GameFile.GuessTarget(storage, fakeVppFile, modDir);
                gameFile.CopyToBackup(false, isUpdate);
                if (!gameFile.Exists)
                {
                    throw new ArgumentException($"ModInfo references nonexistent vpp: [{dataVpp}]");
                }

                var applyResult = await modInstaller.ApplyModInfo(gameFile, vppOps.Value, token);
                var result = new ApplyFileResult(gameFile, applyResult);
                modified.Add(result);
            }
        }

        var individualModFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(x => !x.Directory.IsVppDirectory())
            .Where(x => !excludeDirs.Any(ex => x.Directory.FullName.ToLowerInvariant().StartsWith(ex)))
            .Where(x => !excludeFiles.Contains(x.FullName.ToLowerInvariant()))
            .Where(x => x.IsModContent());

        foreach (var modFile in individualModFiles)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTarget(storage, modFile, modDir);
            gameFile.CopyToBackup(false, isUpdate);
            var applyResult = await modInstaller.ApplyFileMod(gameFile, modFile, token);
            var result = new ApplyFileResult(gameFile, applyResult);
            modified.Add(result);
        }

        var repackVppDirectories = modDir.EnumerateDirectories("*", SearchOption.AllDirectories)
            .Where(x => x.IsVppDirectory())
            .Where(x => x.EnumerateFiles("*", SearchOption.AllDirectories).Any());
        foreach (var vppDir in repackVppDirectories)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTarget(storage, vppDir, modDir);
            gameFile.CopyToBackup(false, isUpdate);
            var applyResult = await modInstaller.ApplyVppDirectoryMod(gameFile, vppDir, token);

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
