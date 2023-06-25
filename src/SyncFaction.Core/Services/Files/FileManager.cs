using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Models.Files;
using SyncFaction.ModManager;

namespace SyncFaction.Core.Services.Files;

public class FileManager
{
    private readonly ModTools modTools;
    private readonly IModInstaller modInstaller;
    private readonly ParallelHelper parallelHelper;

    private readonly ILogger log;

    public FileManager(ModTools modTools, IModInstaller modInstaller, ParallelHelper parallelHelper, ILogger<FileManager> log)
    {
        this.modTools = modTools;
        this.modInstaller = modInstaller;
        this.parallelHelper = parallelHelper;
        this.log = log;
    }

    /// <summary>
    /// Applies mod over current game state. <br />
    /// WARNING: If this is an update, all mods must be reverted and then re-applied
    /// </summary>
    public async Task<ApplyModResult> InstallMod(IGameStorage storage, IMod mod, bool isGog, bool isUpdate, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        var excludeFiles = new HashSet<string>();
        var excludeDirs = new HashSet<string>();
        var modified = new List<ApplyFileResult>();

        var otherVersionSpecificDir = modDir.FileSystem.Path.Combine(modDir.FullName,
            isGog
                ? Constants.SteamModDir
                : Constants.GogModDir);
        excludeDirs.Add(otherVersionSpecificDir);

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

        var individualModFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Directory.IsVppDirectory()).Where(x => !excludeDirs.Any(ex => x.Directory.FullName.ToLowerInvariant().StartsWith(ex, StringComparison.OrdinalIgnoreCase))).Where(x => !excludeFiles.Contains(x.FullName.ToLowerInvariant())).Where(x => x.IsModContent());

        foreach (var modFile in individualModFiles)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTarget(storage, modFile, modDir);
            gameFile.CopyToBackup(false, isUpdate);
            var applyResult = await modInstaller.ApplyFileMod(gameFile, modFile, token);
            var result = new ApplyFileResult(gameFile, applyResult);
            modified.Add(result);
        }

        var repackVppDirectories = modDir.EnumerateDirectories("*", SearchOption.AllDirectories).Where(x => x.IsVppDirectory()).Where(x => x.EnumerateFiles("*", SearchOption.AllDirectories).Any());
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

        var hashFile = modDir.EnumerateFiles("*").FirstOrDefault(x => x.Name.Equals(Constants.HashFile, StringComparison.OrdinalIgnoreCase));
        if (hashFile != null)
        {
            if (!await Verify(storage, modDir, hashFile, token))
            {
                log.LogError("Hash check failed");
                return new ApplyModResult(modified.Select(x => x.GameFile).ToList(), false);
            }
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

    private async Task<bool> Verify(IAppStorage appStorage, IDirectoryInfo modDir, IFileInfo hashFile, CancellationToken token)
    {
        // TODO report what's going on
        var fs = modDir.FileSystem;
        using var stream = hashFile.OpenRead();
        var hashes = JsonSerializer.Deserialize<HashChecks>(stream);
        foreach (var (relativePath, expectedHash) in hashes)
        {
            token.ThrowIfCancellationRequested();
            var filePath = fs.Path.Combine(modDir.FullName, relativePath);
            var file = fs.FileInfo.New(filePath);
            if (!file.Exists)
            {
                return false;
            }

            var hash = await appStorage.ComputeHash(file, token);
            if (hash != expectedHash)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies community update on top of patch. Files are reset to community (if present) or vanilla state before installing. Updates community backup.
    /// </summary>
    public async Task<ApplyModResult> InstallUpdate(IGameStorage storage, List<IMod> pendingUpdates, bool fromScratch, IEnumerable<IMod> installedMods, bool isGog, CancellationToken token)
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
            var result = await InstallMod(storage, update, isGog, true, token);
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
            var result = await InstallMod(storage, mod, isGog, false, token);
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
        var files = storage.EnumerateStockFiles().Concat(storage.EnumeratePatchFiles()).Concat(storage.EnumerateManagedFiles());
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

    public async Task<IReadOnlyList<FileReport>> GenerateFileReport(IAppStorage storage, int threadCount, CancellationToken token)
    {
        var fs = storage.App.FileSystem;
        var entries = storage.Game.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).ToList();
        var results = new ConcurrentBag<FileReport>();
        await parallelHelper.Execute(entries, ReportFile, threadCount, TimeSpan.FromSeconds(5), "Computing hashes", "files", token);
        return results.ToList();

        async Task ReportFile(IFileSystemInfo info, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            var relativePath = fs.Path.GetRelativePath(storage.Game.FullName, info.FullName);
            var created = info.CreationTimeUtc;
            var modified = info.LastWriteTimeUtc;
            var accessed = info.LastAccessTimeUtc;
            switch (info)
            {
                case IDirectoryInfo:
                    results.Add(new FileReport(relativePath + "/", -1, null, created, modified, accessed));

                    break;
                case IFileInfo file:
                    var size = file.Length;
                    var hash = await storage.ComputeHash(file, t);
                    results.Add(new FileReport(relativePath, size, hash, created, modified, accessed));

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(info));
            }
        }
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
