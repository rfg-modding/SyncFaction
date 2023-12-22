using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Models.Files;

namespace SyncFaction.Core.Services.Files;

public class FileManager
{
    private readonly IModInstaller modInstaller;
    private readonly ParallelHelper parallelHelper;
    private readonly FileChecker fileChecker;

    private readonly ILogger<FileManager> log;

    public FileManager(IModInstaller modInstaller, ParallelHelper parallelHelper, FileChecker fileChecker, ILogger<FileManager> log)
    {
        this.modInstaller = modInstaller;
        this.parallelHelper = parallelHelper;
        this.fileChecker = fileChecker;
        this.log = log;
    }

    /// <summary>
    /// Applies mod over current game state. <br />
    /// WARNING: If this is an update, all mods must be reverted and then re-applied
    /// </summary>
    public async Task<ApplyModResult> InstallMod(IGameStorage storage, IMod mod, bool isGog, bool isUpdate, int threadCount, CancellationToken token)
    {
        log.LogInformation("Installing `{id}` {name}", mod.Id, mod.Name);
        var modDir = storage.GetModDir(mod);
        var excludeFiles = new HashSet<string>();
        var excludeDirs = new HashSet<string>();
        var modified = new List<ApplyFileResult>();

        var otherVersionSpecificDir = modDir.FileSystem.Path.Combine(modDir.FullName,
            isGog
                ? Constants.SteamModDir
                : Constants.GogModDir);
        excludeDirs.Add(otherVersionSpecificDir);
        log.LogTrace("Excluded other-version-specific dir from mod content [{dir}]", otherVersionSpecificDir);

        if (mod.ModInfo is not null)
        {
            log.LogTrace("Mod [{id}] has modinfo.xml", mod.Id);
            var operations = mod.ModInfo.BuildOperations();
            var modinfoPath = modDir.EnumerateFiles(Constants.ModInfoFile, SearchOption.AllDirectories).First().FullName.ToLowerInvariant();
            excludeFiles.Add(modinfoPath);
            log.LogTrace("Excluded modinfo file from mod content [{file}]", modinfoPath);
            foreach (var op in operations.FileSwaps)
            {
                // NOTE: this won't exclude files mentioned in selectbox inputs!
                var file = op.Target.FullName.ToLowerInvariant();
                excludeFiles.Add(file);
                log.LogTrace("Excluded modinfo referenced file from mod content [{file}]", file);
            }

            // exclude dir with modinfo.xml, recursively
            // if it's mod root, ignore it anyway, to avoid clutter when using old mods
            var modInfoDir = mod.ModInfo.WorkDir.FullName.ToLowerInvariant();
            excludeDirs.Add(modInfoDir);
            log.LogTrace("Excluded modinfo directory from mod content [{dir}]", modInfoDir);

            foreach (var (dataVpp, ops) in operations.VppOperations)
            {
                var fakeVppFile = modDir.FileSystem.FileInfo.New(modDir.FileSystem.Path.Combine(modDir.FullName, dataVpp));
                var gameFile = GameFile.GuessTarget(storage, fakeVppFile, modDir, log);
                log.LogTrace("Mod [{id}]: editing [{vpp}], guessed as [{file}]", mod.Id, dataVpp, gameFile.AbsolutePath);
                gameFile.CopyToBackup(false, isUpdate);
                if (!gameFile.Exists)
                {
                    throw new ArgumentException($"ModInfo references nonexistent vpp: [{dataVpp}]");
                }

                var applyResult = await modInstaller.ApplyModInfo(gameFile, ops, token);
                var result = new ApplyFileResult(gameFile, applyResult);
                modified.Add(result);
            }
        }

        var individualModFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(static x => !x.Directory!.IsVppDirectory())
            .Where(x => !excludeDirs.Any(ex => x.Directory!.FullName.ToLowerInvariant().StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !excludeFiles.Contains(x.FullName.ToLowerInvariant()))
            .Where(static x => x.IsModContent());
        foreach (var modFile in individualModFiles)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTarget(storage, modFile, modDir, log);
            log.LogTrace("Mod [{id}] has file [{file}], guessed as [{gameFile}]", mod.Id, modFile.FullName, gameFile.RelativePath);
            gameFile.CopyToBackup(false, isUpdate);
            var applyResult = await modInstaller.ApplyFileMod(gameFile, modFile, token);
            var result = new ApplyFileResult(gameFile, applyResult);
            modified.Add(result);
        }

        var repackVppDirectories = modDir.EnumerateDirectories("*", SearchOption.AllDirectories)
            .Where(x => !excludeDirs.Any(ex => x.FullName.ToLowerInvariant().StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
        .Where(x => x.IsVppDirectory()).Where(x => x.EnumerateFiles("*", SearchOption.AllDirectories).Any());
        foreach (var vppDir in repackVppDirectories)
        {
            token.ThrowIfCancellationRequested();
            var gameFile = GameFile.GuessTarget(storage, vppDir, modDir, log);
            log.LogTrace("Mod [{id}] has dir [{dir}], guessed as loose vpp [{gameFile}]", mod.Id, vppDir.FullName, gameFile.RelativePath);
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

        if (modified.Any(static x => !x.Success))
        {
            log.LogError("Some mod files failed to apply");
            return new ApplyModResult(modified.Select(x => x.GameFile).ToList(), false);
        }

        var hashFile = modDir.EnumerateFiles("*").FirstOrDefault(static x => x.Name.Equals(Constants.HashFile, StringComparison.OrdinalIgnoreCase));
        if (hashFile != null)
        {
            log.LogTrace("Mod [{id}] has hash file [{file}]", mod.Id, hashFile.FullName);
            if (!await Verify(storage, hashFile, threadCount, token))
            {
                log.LogError("Hash check failed");
                return new ApplyModResult(modified.Select(static x => x.GameFile).ToList(), false);
            }
        }

        if (isUpdate)
        {
            log.LogTrace("Mod [{id}] is update, creating patch backup", mod.Id);

            foreach (var result in modified)
            {
                // store patched files as patch bak, overwrite existing with new version
                token.ThrowIfCancellationRequested();
                result.GameFile.CopyToBackup(true, true);
            }
        }

        return new ApplyModResult(modified.Select(static x => x.GameFile).ToList(), true);
    }

    private async Task<bool> Verify(IAppStorage storage, IFileInfo hashFile, int threadCount, CancellationToken token)
    {
        var fs = storage.FileSystem;
        await using var stream = hashFile.OpenRead();
        var hashes = JsonSerializer.Deserialize<HashChecks>(stream)!;
        return await parallelHelper.Execute(hashes.ToList(), Body, threadCount, TimeSpan.FromSeconds(10), "Verifying", "files", token);

        async Task Body(KeyValuePair<string, string> hashChecks, CancellationTokenSource breaker, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var relativePath = hashChecks.Key;
            var expectedHash = hashChecks.Value;
            var filePath = fs.Path.Combine(storage.Game.FullName, relativePath);
            var file = fs.FileInfo.New(filePath);
            if (!file.Exists)
            {
                log.LogError("File for verification not found: [{}]", file.FullName);
                breaker.Cancel();
            }

            var hash = await fileChecker.ComputeHash(file, token);
            if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                log.LogError("File [{}] SHA256=[{}], expected=[{}]", file.FullName, hash, expectedHash);
                breaker.Cancel();
            }
        }
    }

    /// <summary>
    /// Applies updates on top of patch. Files are reset to patch (if present) or vanilla state before installing. Updates patch backup.
    /// </summary>
    public async Task<ApplyModResult> InstallUpdate(IGameStorage storage, List<IMod> pendingUpdates, bool fromScratch, List<IMod> installedMods, bool isGog, int threadCount, CancellationToken token)
    {
        Rollback(storage, fromScratch, token);
        if (fromScratch)
        {
            ForgetUpdates(storage);
        }

        log.LogInformation("Installing updates: `{updates}`", string.Join(", ", pendingUpdates.Select(static x => x.Id)));
        var modifiedFiles = new List<GameFile>();
        foreach (var update in pendingUpdates)
        {
            token.ThrowIfCancellationRequested();
            var result = await InstallMod(storage, update, isGog, true, threadCount, token);
            modifiedFiles.AddRange(result.ModifiedFiles);
            if (!result.Success)
            {
                log.LogError("Install update {id} failed", update.Id);
                return new ApplyModResult(modifiedFiles, false);
            }
        }

        log.LogInformation("Re-installing mods: `{mods}`", string.Join(", ", installedMods.Select(static x => x.Id)));
        foreach (var mod in installedMods)
        {
            token.ThrowIfCancellationRequested();
            var result = await InstallMod(storage, mod, isGog, false, threadCount, token);
            modifiedFiles.AddRange(result.ModifiedFiles);
            if (!result.Success)
            {
                log.LogError("Re-install mod {id} failed", mod.Id);
                return new ApplyModResult(modifiedFiles, false);
            }
        }

        return new ApplyModResult(modifiedFiles, true);
    }

    /// <summary>
    /// Restores original files, from vanilla or terraform backup
    /// </summary>
    public void Rollback(IGameStorage storage, bool toVanilla, CancellationToken token)
    {
        log.LogInformation("Restoring files to {state}",
            toVanilla
                ? "vanilla"
                : "latest patch");
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

        // NOTE: don't automatically nuke patch_bak if restored to vanilla. this allows fast switch between vanilla and updated version
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended")]
    public async Task<IReadOnlyList<FileReport>> GenerateFileReport(IAppStorage storage, int threadCount, CancellationToken token)
    {
        var fs = storage.App.FileSystem;
        var entries = storage.Game.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).ToList();
        var results = new ConcurrentBag<FileReport>();
        await parallelHelper.Execute(entries, ReportFile, threadCount, TimeSpan.FromSeconds(5), "Computing hashes", "files", token);
        return results.ToList();

        async Task ReportFile(IFileSystemInfo info, CancellationTokenSource breaker, CancellationToken t)
        {
            var relativePath = string.Empty;
            var created = DateTime.MinValue;
            var modified = DateTime.MinValue;
            var accessed = DateTime.MinValue;
            try
            {
                relativePath = fs.Path.GetRelativePath(storage.Game.FullName, info.FullName);
                created = info.CreationTimeUtc;
                modified = info.LastWriteTimeUtc;
                accessed = info.LastAccessTimeUtc;
                switch (info)
                {
                    case IDirectoryInfo:
                        results.Add(new FileReport(relativePath + "/", -1, null, created, modified, accessed));

                        break;
                    case IFileInfo file:
                        var size = file.Length;
                        var hash = await fileChecker.ComputeHash(file, t);
                        results.Add(new FileReport(relativePath, size, hash, created, modified, accessed));

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(info));
                }
            }
            catch (Exception e)
            {
                results.Add(new FileReport(relativePath, -1, $"FAIL: {e.GetType().Name}", created, modified, accessed));
            }
        }
    }

    /// <summary>
    /// When terraform patch is diverged with installed things, nuke them to install from scratch. Otherwise, install only what's new, using patch_bak
    /// </summary>
    internal void ForgetUpdates(IGameStorage storage)
    {
        log.LogInformation("Removing unneeded updates");
        storage.PatchBak.Delete(true);
        storage.PatchBak.Create();
    }

    /// <summary>
    /// Returns first nonexistent file with .bak extension and a number, eg: foo.bar.bak, foo.bar.bak.1, foo.bar.2, ...
    /// </summary>
    public IFileInfo GetUniqueBakFile(IFileInfo file)
    {
        var fs = file.FileSystem;
        var i = 0;
        var result = fs.FileInfo.New(file.FullName + ".bak");
        while (result.Exists)
        {
            i++;
            result = fs.FileInfo.New(result.FullName + $".{i}");
        }

        log.LogTrace("Unique bak file: [{result}]", result.FullName);
        return result;
    }
}
