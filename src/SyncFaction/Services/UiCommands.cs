using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HTMLConverter;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using SyncFaction.Core;
using SyncFaction.Core.Models;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Models.Files;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Extras;
using SyncFaction.Models;
using SyncFaction.ModManager.XmlModels;
using SyncFaction.ViewModels;

namespace SyncFaction.Services;

public class UiCommands
{
    private readonly ILogger<UiCommands> log;
    private readonly IFileSystem fileSystem;
    private readonly ModLoader modLoader;
    private readonly IStateProvider stateProvider;
    private readonly FfClient ffClient;
    private readonly AppInitializer appInitializer;
    private readonly FileManager fileManager;
    private readonly ParallelHelper parallelHelper;
    private readonly FileChecker fileChecker;
    private readonly SteamLocator steamLocator;
    private readonly GogLocator gogLocator;

    public UiCommands(IFileSystem fileSystem, ModLoader modLoader, IStateProvider stateProvider, FfClient ffClient, AppInitializer appInitializer, FileManager fileManager, ParallelHelper parallelHelper, FileChecker fileChecker, SteamLocator steamLocator, GogLocator gogLocator, ILogger<UiCommands> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.modLoader = modLoader;
        this.stateProvider = stateProvider;
        this.ffClient = ffClient;
        this.appInitializer = appInitializer;
        this.fileManager = fileManager;
        this.parallelHelper = parallelHelper;
        this.fileChecker = fileChecker;
        this.steamLocator = steamLocator;
        this.gogLocator = gogLocator;
    }

    internal async Task<bool> Download(ViewModel viewModel, CancellationToken token)
    {
        var mods = viewModel.LockCollections(() => viewModel.OnlineMods.Where(static x => x.Selected).ToList());

        // sanity check
        if (mods.Count != viewModel.OnlineSelectedCount)
        {
            throw new InvalidOperationException($"Collection length {mods.Count} != SelectedCount {viewModel.OnlineSelectedCount}");
        }

        await parallelHelper.Execute(mods, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Downloading", "mods", token);
        return await RefreshLocal(viewModel, token);

        async Task Body(OnlineModViewModel mvm, CancellationTokenSource breaker, CancellationToken t)
        {
            // display "in progress" regardless of real mod status
            mvm.Status = OnlineModStatus.InProgress;
            var mod = mvm.Mod;
            var storage = viewModel.Model.GetGameStorage(fileSystem, log);
            var modDir = storage.GetModDir(mod);
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, t);
            mvm.Status = mod.Status; // status is changed by ffClient
            if (!clientSuccess)
            {
                log.LogError("Downloading mod failed: [{id}] {name}", mod.Id, mod.Name);
                breaker.Cancel();
            }
        }
    }

    internal async Task<bool> Apply(ViewModel viewModel, CancellationToken token)
    {
        await ChangePlayerName(viewModel, token);
        var modsToApply = viewModel.LockCollections(() => viewModel.LocalMods.Where(static x => x.Status == LocalModStatus.Enabled).ToList());
        await RestoreInternal(viewModel, false, token);
        foreach (var mvm in modsToApply.Where(static mvm => mvm.Mod.ModInfo is not null))
        {
            log.LogTrace("Saving modinfo.xml settings for [{mod}]", mvm.Mod.Id);
            viewModel.Model.Settings.Mods[mvm.Mod.Id] = mvm.Mod.ModInfo!.SaveCurrentSettings();
            mvm.Mod.ModInfo.ApplyUserInput();
        }

        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var threadCount = viewModel.Model.ThreadCount;
        foreach (var vm in modsToApply)
        {
            token.ThrowIfCancellationRequested();
            var result = await fileManager.InstallMod(storage, vm.Mod, viewModel.Model.IsGog!.Value, false, threadCount, token);
            if (!result.Success)
            {
                log.LogTrace("Installation failed for [{id}]", vm.Mod.Id);
                return false;
            }
        }

        viewModel.LockCollections(() =>
        {
            viewModel.Model.AppliedMods.Clear();
            viewModel.Model.LastMods.Clear();
            foreach (var vm in modsToApply)
            {
                viewModel.Model.AppliedMods.Add(vm.Mod.Id);
                // also save backup of current mods
                viewModel.Model.LastMods.Add(vm.Mod.Id);
            }
        });
        return true;
    }

    internal async Task<bool> Display(ViewModel viewModel, CancellationToken token)
    {
        var isLocal = viewModel.SelectedTab == Tab.Apply;
        var mvm = viewModel.SelectedMod;

        if (mvm is null)
        {
            log.LogWarning("Attempt to display mod when it's null");
            return false;
        }

        if (mvm.Selected is not true)
        {
            log.LogWarning("Attempt to display wrong mod: [{id}]", mvm.Mod.Id);
            return false;
        }

        log.Clear();
        log.LogInformation(Md.NoScroll.Id(), "{markdown}", mvm.Mod.Markdown);
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        DisplayExtendedInfo(mvm.Mod, isLocal, storage);
        return true;
    }

    private void DisplayExtendedInfo(IMod mod, bool isLocal, GameStorage storage)
    {
        log.LogInformation((Md.NoScroll).Id(), "---");
        log.LogInformation((Md.NoScroll).Id(), "Id: `{id}`", mod.Id);
        if (isLocal)
        {
            var modDir = storage.GetModDir(mod);
            log.LogInformation((Md.NoScroll).Id(), "Path: `{path}`", modDir.FullName);
            log.LogInformation((Md.NoScroll).Id(), "[Open in Explorer]({dir})", modDir.FullName);
        }

        if (mod.Category is not Category.Dev and not Category.Local)
        {
            log.LogInformation((Md.NoScroll).Id(), "[See on FactionFiles]({url})", mod.BrowserUrl);
            log.LogInformation((Md.NoScroll).Id(), "Uploaded: `{date:yyyy/MM/dd}`", mod.CreatedAt);
            log.LogInformation((Md.NoScroll).Id(), "Downloads: `{downloads}`", mod.DownloadCount);
        }

        if (!isLocal)
        {
            return;
        }

        log.LogInformation((Md.NoScroll | Md.B).Id(), "Extended info:");
        if (mod.Flags == ModFlags.None)
        {
            log.LogInformation((Md.NoScroll | Md.Bullet).Id(), "Mod has no known files and probably will do nothing");
            return;
        }

        if (mod.Flags.HasFlag(ModFlags.AffectsMultiplayerFiles))
        {
            log.LogInformation((Md.NoScroll | Md.Bullet).Id(), "Mod will edit **multiplayer files**. If you experience issues playing with others, disable it");
        }

        if (mod.Flags.HasFlag(ModFlags.HasXDelta))
        {
            log.LogInformation((Md.NoScroll | Md.Bullet).Id(), "Mod has binary patches. It will only work on files in certain state, eg. when they are unmodified. If you experience issues, try to change mod order, placing this one before others");
        }

        if (mod.Flags.HasFlag(ModFlags.HasModInfo))
        {
            log.LogInformation((Md.NoScroll | Md.Bullet).Id(), "Mod is in ModManger format. If it has user-defined settings, they are on the right. Some ModManager-style mods can be incompatible with one another or overwrite each other's edits");
        }

        if (mod.Flags.HasFlag(ModFlags.HasReplacementFiles))
        {
            log.LogInformation((Md.NoScroll | Md.Bullet).Id(), "Mod replaces certain files entirely. If they were edited by other mod before, changes will be lost. If you experience issues, try to change mod order, placing this one before others");
        }

        log.LogInformation((Md.NoScroll).Id(), "---");
    }

    internal async Task<bool> RestoreMods(ViewModel viewModel, CancellationToken token)
    {
        log.LogTrace("Loading mods from backup list");
        viewModel.LockCollections(() =>
        {
            // load from backup list
            viewModel.Model.AppliedMods.Clear();
            viewModel.Model.AppliedMods.AddRange(viewModel.Model.LastMods);
        });
        viewModel.UpdateModSelection();
        log.LogTrace("Applying selected mods");
        await Apply(viewModel, token);
        return true;
    }

    internal async Task<bool> RestorePatch(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, false, token);
        viewModel.UpdateModSelection();
        return true;
    }

    internal async Task<bool> RestoreVanilla(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, true, token);
        viewModel.UpdateModSelection();
        return true;
    }

    private async Task RestoreInternal(ViewModel viewModel, bool toVanilla, CancellationToken token)
    {
        log.LogTrace("Restoring files to {state}",
            toVanilla
                ? "vanilla"
                : "patch");
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        fileManager.Rollback(storage, toVanilla, token);

        viewModel.LockCollections(() =>
        {
            viewModel.Model.AppliedMods.Clear();
            if (toVanilla)
            {
                // forget we had updates entirely
                viewModel.Model.TerraformUpdates.Clear();
                viewModel.Model.ReconstructorUpdates.Clear();
            }
        });
    }

    internal async Task<bool> Init(ViewModel viewModel, CancellationToken token)
    {
        if (await appInitializer.Init(viewModel, token) == false)
        {
            return false;
        }

        return await InitPopulateData(viewModel, token);
    }

    private async Task<bool> InitPopulateData(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem, log);
        // create dirs and validate files if required
        gameStorage.InitBakDirectories();
        if (viewModel.Model.IsVerified != true && !await fileChecker.CheckGameFiles(gameStorage, viewModel.Model.ThreadCount, token))
        {
            log.LogError("Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.");
            log.LogError("Verify/reinstall your game, then run SyncFaction again.");
            log.LogInformation(Md.I.Id(), "See you later miner!");
            return false;
        }

        viewModel.Model.IsVerified = true;
        if (viewModel.Model.StartupUpdates == false)
        {
            log.LogWarning("Skipped update check because StartupUpdates is disabled");
        }
        else
        {
            // populate patch info
            await CheckPatchUpdates(viewModel, token);
            if (viewModel.UpdateRequired)
            {
                // we need to update first, don't populate mods
                return true;
            }
        }

        // populate mod list and stuff
        return await RefreshLocal(viewModel, token) && await RefreshOnline(viewModel, true, token);
    }

    internal async Task<bool> Run(ViewModel viewModel, CancellationToken token)
    {
        if (viewModel.Model.IsGog is null)
        {
            throw new InvalidOperationException("App is not properly initialized, still don't know game version");
        }

        await ChangePlayerName(viewModel, token);

        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var launcher = storage.Game.EnumerateFiles().SingleOrDefault(static x => x.Name.Equals("launcher.exe", StringComparison.OrdinalIgnoreCase));
        if (launcher?.Exists == true)
        {
            log.LogInformation("Launching game via Reconstructor Launcher...");
            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = launcher.FullName,
                WorkingDirectory = launcher.Directory.FullName
            });
            return true;
        }

        switch (viewModel.Model.IsGog)
        {
            case true:
                {
                    var exe = storage.Game.EnumerateFiles().Single(static x => x.Name.Equals("rfg.exe", StringComparison.OrdinalIgnoreCase));
                    log.LogInformation("Launching game via exe...");
                    Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = exe.FullName
                    });
                    return true;
                }
            default:
                log.LogInformation("Launching game via Steam...");
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = "steam://rungameid/667720"
                });
                return true;
        }
    }

    internal async Task<bool> RefreshOnline(ViewModel viewModel, CancellationToken token) => await RefreshOnline(viewModel, false, token);

    private async Task<bool> RefreshOnline(ViewModel viewModel, bool isInit, CancellationToken token)
    {
        // always show mods from local directory
        var categories = new List<Category> { Category.Local };
        if (viewModel.Model.DevHiddenMods)
        {
            log.LogWarning("Listing dev mods from CDN because DevHiddenMods is enabled");
            categories.Add(Category.Dev);
        }

        Action displayLast = null;
        if (viewModel.Model.StartupUpdates == false && isInit)
        {
            log.LogWarning("Skipped reading mods and news from FactionFiles because StartupUpdates is disabled");
        }
        else
        {
            // add categories to query
            categories.Add(Category.ModsGeneral);
            categories.Add(Category.ModsScriptLoader);

            // upd text
            using var document = await ffClient.GetNewsWiki(token);
            var header = document.GetElementById("firstHeading")?.TextContent;
            var content = document.GetElementById("mw-content-text")?.InnerHtml;
            var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
            displayLast = () =>
            {
                log.Clear();
                log.LogInformation((Md.NoScroll | Md.H1).Id(), "{header}\n\n", header);
                log.LogInformation((Md.NoScroll | Md.Xaml).Id(), "{xaml}", xaml);
            };
        }

        // upd list
        viewModel.LockCollections(() =>
        {
            viewModel.OnlineMods.Clear();
            viewModel.OnlineSelectedCount = 0;
        });

        var result = await parallelHelper.Execute(categories, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Listing mods", "categories", token);
        Task.Yield();
        if (!token.IsCancellationRequested)
        {
            displayLast?.Invoke();
        }

        return result;

        async Task Body(Category category, CancellationTokenSource breaker, CancellationToken t)
        {
            var mods = await modLoader.GetMods(category, viewModel.Model.GetGameStorage(fileSystem, log), t);
            viewModel.AddOnlineMods(mods);
        }
    }

    internal async Task<bool> RefreshLocal(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var mods = await modLoader.GetAvailableMods(viewModel.Model.Settings, viewModel.Model.DevHiddenMods, storage, token);
        viewModel.UpdateLocalMods(mods);
        return true;
    }

    internal async Task<bool> Update(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem, log);
        var mods = new ConcurrentBag<IMod>();
        var cats = new List<Category>
        {
            Category.Tools,
            Category.ModsGeneral
        };
        await Task.WhenAll(cats.Select(async c =>
            {
                var result = await modLoader.GetMods(c, gameStorage, token);
                foreach (var mod in result)
                {
                    mods.Add(mod);
                }
            })
            .ToArray());

        var updates = viewModel.LockCollections(() => viewModel.Model.RemoteTerraformUpdates.Concat(viewModel.Model.RemoteReconstructorUpdates).Select(x => mods.Single(y => y.Id == x)).ToList());

        var installed = viewModel.LockCollections(() => viewModel.Model.TerraformUpdates.Concat(viewModel.Model.ReconstructorUpdates).ToList());
        var result = await InstallUpdates(installed, updates, gameStorage, viewModel, token);
        if (!result.Success)
        {
            log.LogError("Failed to update game to latest patch. SyncFaction can't work until you restore all files to their default state.");
            log.LogError("Verify/reinstall your game, then run SyncFaction again.");
            log.LogInformation(Md.I.Id(), "See you later miner!");
            return false;
        }

        viewModel.LockCollections(() =>
        {
            viewModel.Model.TerraformUpdates.Clear();
            viewModel.Model.TerraformUpdates.AddRange(viewModel.Model.RemoteTerraformUpdates);
            viewModel.Model.ReconstructorUpdates.Clear();
            viewModel.Model.ReconstructorUpdates.AddRange(viewModel.Model.RemoteReconstructorUpdates);
        });
        stateProvider.WriteStateFile(gameStorage, viewModel.Model.ToState(), log);
        log.LogInformation("Successfully updated game: `{version}`", viewModel.GetHumanReadableVersion());
        viewModel.UpdateRequired = false;

        // populate mod list and stuff
        return await RefreshLocal(viewModel, token) && await RefreshOnline(viewModel, token);
    }

    private async Task CheckPatchUpdates(ViewModel viewModel, CancellationToken token)
    {
        log.LogInformation("Installed patches: `{version}`", viewModel.GetHumanReadableVersion());
        log.LogInformation("Checking for updates...");
        var terraform = await ffClient.ListPatchIds(Constants.PatchSearchStringPrefix, token);
        var reconstructor = await ffClient.ListPatchIds(Constants.ReconstructorSearchStringPrefix, token);
        viewModel.UpdateUpdates(terraform, reconstructor);
    }

    internal void WriteState(ViewModel viewModel)
    {
        if (string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            log.LogTrace("Game directory is empty, nowhere to save state");
            return;
        }

        var appStorage = new AppStorage(viewModel.Model.GameDirectory, fileSystem, log);
        stateProvider.WriteStateFile(appStorage, viewModel.Model.ToState(), log);
    }

    internal void ModResetInputs(ModInfo modInfo, ViewModel viewModel)
    {
        foreach (var listBox in modInfo.UserInput.OfType<ListBox>())
        {
            listBox.SelectedIndex = 0;
            foreach (var displayOption in listBox.DisplayOptions)
            {
                switch (displayOption)
                {
                    case CustomOption c:
                        c.Value = null;
                        break;
                }
            }
        }

        var mod = viewModel.SelectedMod!.Mod;
        viewModel.Model.Settings.Mods.Remove(mod.Id);
        log.LogTrace("Removed settings for mod [{id}]", mod.Id);
    }

    internal async Task<bool> GetLogs(ViewModel viewModel, CancellationToken token)
    {
        log.Clear();
        log.LogTrace("Getting all logs, erasing memory list and UI...");
        var logger = (MemoryTarget) LogManager.Configuration.AllTargets.Single(static x => x.Name == "memory");
        logger.Flush(static e => throw e);
        var memoryLogs = logger.Logs.ToList();
        var state = viewModel.Model.ToState();
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        viewModel.DiagView = json + "\n\n" + string.Join("\n", memoryLogs);
        logger.Logs.Clear();
        return true;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended")]
    internal async Task<bool> GenerateReport(ViewModel viewModel, CancellationToken token)
    {
        try
        {
            var storage = viewModel.Model.GetGameStorage(fileSystem, log);
            var fileReports = await fileManager.GenerateFileReport(storage, viewModel.Model.ThreadCount, token);
            var filesDict = fileReports.ToDictionary(static x => x.Path.Replace('\\', '/').PadRight(100), static x => x.ToString());
            var files = new SortedDictionary<string, string>(filesDict);
            var state = viewModel.Model.ToState();
            var report = new Report(state, Title.Value, viewModel.LastException);
            var logger = (MemoryTarget) LogManager.Configuration.AllTargets.Single(static x => x.Name == "memory");
            var memoryLogs = logger.Logs;
            var filesJson = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            viewModel.DiagView = filesJson + "\n\n" + reportJson + "\n\n" + string.Join("\n", memoryLogs);
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Can not collect diagnostics, something is completely broken!");
            viewModel.DiagView = e.ToString();
            return false;
        }
    }

    private async Task<ApplyModResult> InstallUpdates(IEnumerable<long> installed, IReadOnlyCollection<IMod> updates, IGameStorage storage, ViewModel viewModel, CancellationToken token)
    {
        var updateIds = updates.Select(static x => x.Id).ToList();
        var filteredUpdateIds = installed.FilterUpdateList(updateIds).ToHashSet();
        var fromScratch = filteredUpdateIds.Count == updateIds.Count;

        var pendingUpdates = updates.Where(x => filteredUpdateIds.Contains(x.Id)).ToList();
        log.LogTrace("Updates to install: [{updates}]", string.Join(", ", pendingUpdates.Select(static x => x.Id)));

        var success = await parallelHelper.Execute(pendingUpdates, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Downloading updates", "update", token);
        if (!success)
        {
            return new ApplyModResult(new List<GameFile>(), false);
        }

        var refreshSuccess = await RefreshLocal(viewModel, token);
        if (!refreshSuccess)
        {
            return new ApplyModResult(new List<GameFile>(), false);
        }

        var installedMods = viewModel.Model.AppliedMods.Select(x => viewModel.LocalMods.First(m => m.Mod.Id == x).Mod).ToList();
        var result = await fileManager.InstallUpdate(storage, pendingUpdates, fromScratch, installedMods, viewModel.Model.IsGog!.Value, viewModel.Model.ThreadCount, token);
        return result;

        async Task Body(IMod mod, CancellationTokenSource breaker, CancellationToken t)
        {
            var modDir = storage.GetModDir(mod);
            mod.Hide = true; // NOTE: patches are not intended to be installed as normal mods, store them as hidden
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, t);
            if (!clientSuccess)
            {
                log.LogError("Downloading update {update} failed", mod.Id);
                breaker.Cancel();
            }
        }
    }

    internal async Task<bool> CopySaveToGog(ViewModel viewModel, CancellationToken token) => await CopySave(true, token);

    internal async Task<bool> CopySaveToSteam(ViewModel viewModel, CancellationToken token) => await CopySave(false, token);

    private async Task<bool> CopySave(bool steamToGog, CancellationToken token)
    {
        var direction = steamToGog
            ? "Steam to GOG"
            : "GOG to Steam";
        var srcName = steamToGog
            ? "Steam"
            : "GOG";
        var dstName = steamToGog
            ? "GOG"
            : "Steam";
        log.LogTrace("CopySave: {direction}", direction);

        async Task<(IFileInfo? src, IFileInfo? dst)> GetPaths()
        {
            var steamPath = await steamLocator.DetectSteamSavegameFile(token);
            var gogPath = gogLocator.DetectGogSavegameFile();
            return steamToGog
                ? (steamPath, gogPath)
                : (gogPath, steamPath);
        }

        var (src, dst) = await GetPaths();
        if (src?.Exists != true)
        {
            log.LogInformation("{srcName} savegame not found, nothing to copy", srcName);
            return true; // this is fine
        }

        if (dst is null)
        {
            log.LogError("{dsnName} savegame location is unknown, can't copy to this destination", dstName);
            return true; // this is fine
        }

        if (dst.Exists)
        {
            var bak = fileManager.GetUniqueBakFile(dst);
            dst.CopyTo(bak.FullName);
        }

        var dir = dst.Directory!;
        if (!dir.Exists)
        {
            dir.Create();
            log.LogTrace("Created directory [{dir}]", dir.FullName);
        }

        src.CopyTo(dst.FullName, true);
        log.LogTrace("CopySave {direction} success, [{src}] to [{dst}]", direction, src.FullName, dst.FullName);
        return true;
    }

    private async Task ChangePlayerName(ViewModel model, CancellationToken token)
    {
        var newName = model.Model.PlayerName;
        if (string.IsNullOrWhiteSpace(newName))
        {
            log.LogDebug("Player name has not changed");
            return;
        }

        log.LogInformation("Changing player name to [{name}]", newName);
        var config = gogLocator.GetConfigFile();
        if (!config.Exists)
        {
            log.LogWarning("Can not change player name: config file does not exist. Run game once, then set player name again");
            return;
        }

        var bak = fileManager.GetUniqueBakFile(config);
        config.CopyTo(bak.FullName);
        var content = await fileSystem.File.ReadAllTextAsync(config.FullName, token);
        var replacement = $"multiplayer_user_name={newName}";
        var regex = new Regex("multiplayer_user_name=(.*)");
        var newContent = regex.Replace(content, replacement);
        await fileSystem.File.WriteAllTextAsync(config.FullName, newContent, token);
        model.Model.PlayerName = string.Empty;
        log.LogDebug("Player name changed to [{name}]", newName);
    }
}
