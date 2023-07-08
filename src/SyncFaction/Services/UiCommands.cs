using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
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

    public UiCommands(IFileSystem fileSystem, ModLoader modLoader, IStateProvider stateProvider, FfClient ffClient, AppInitializer appInitializer, FileManager fileManager, ParallelHelper parallelHelper, FileChecker fileChecker, ILogger<UiCommands> log)
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
    }

    internal async Task<bool> Download(ViewModel viewModel, CancellationToken token)
    {
        var mods = viewModel.LockCollections(() => viewModel.OnlineMods.Where(x => x.Selected).ToList());

        // sanity check
        if (mods.Count != viewModel.OnlineSelectedCount)
        {
            throw new InvalidOperationException($"Collection length {mods.Count} != SelectedCount {viewModel.OnlineSelectedCount}");
        }

        await parallelHelper.Execute(mods, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Downloading {mods.Count} mods", "mods", token);
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
                log.LogError("Downloading mod failed");
                breaker.Cancel();
                return;
            }

            var files = string.Join(", ", modDir.GetFiles().Select<IFileInfo, string>(x => $"`{x.Name}`"));
            //log.LogDebug($"Mod contents: {files}");
        }
    }

    internal async Task<bool> Apply(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, false, token);
        var modsToApply = viewModel.LockCollections(() => viewModel.LocalMods.Where(x => x.Status == LocalModStatus.Enabled).ToList());
        foreach (var mvm in modsToApply)
        {
            if (mvm.Mod.ModInfo is not null)
            {
                viewModel.Model.Settings.Mods[mvm.Mod.Id] = mvm.Mod.ModInfo.SaveCurrentSettings();
                mvm.Mod.ModInfo.ApplyUserInput();
            }
        }

        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var threadCount = viewModel.Model.ThreadCount;
        foreach (var vm in modsToApply)
        {
            var result = await fileManager.InstallMod(storage, vm.Mod, viewModel.Model.IsGog!.Value, false, threadCount, token);

            if (!result.Success)
            {
                return false;
            }
        }

        viewModel.LockCollections(() =>
        {
            foreach (var vm in modsToApply)
            {
                viewModel.Model.AppliedMods.Add(vm.Mod.Id);
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
            //log.LogWarning($"Attempt to display wrong mod: {mvm.Name}");
            return false;
        }

        log.Clear();
        //log.LogInformation(new EventId(0, "log_false"), mvm.Mod.Markdown);
        if (isLocal)
        {
            //log.LogInformation(new EventId(0, "log_false"), "\n---\n\n" + mvm.Mod.InfoMd());
        }

        if (mvm is LocalModViewModel lvm)
        {
            //viewModel.XmlView2 = JsonConvert.SerializeObject(lvm.Mod.ModInfo, Formatting.Indented);
        }

        return true;
    }

    internal async Task<bool> RestoreMods(ViewModel viewModel, CancellationToken token)
    {
        // TODO
        throw new NotImplementedException();
    }

    internal async Task<bool> RestorePatch(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, false, token);
        return true;
    }

    internal async Task<bool> RestoreVanilla(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, true, token);
        return true;
    }

    private async Task RestoreInternal(ViewModel viewModel, bool toVanilla, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        fileManager.Rollback(storage, toVanilla, token);
        viewModel.Model.AppliedMods.Clear();
        if (toVanilla)
        {
            viewModel.LockCollections(() =>
            {
                // forget we had updates entirely
                viewModel.Model.TerraformUpdates.Clear();
                viewModel.Model.RslUpdates.Clear();
            });
        }
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
        if (viewModel.Model.DevMode)
        {
            log.LogWarning("Skipped update check because DevMode is enabled");
        }
        else
        {
            // populate community patch info
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

    internal Task<bool> Run(ViewModel viewModel, CancellationToken token)
    {
        switch (viewModel.Model.IsGog)
        {
            case null:
                throw new InvalidOperationException("App is not properly initialized, still don't know game version");
            case true:
                {
                    log.LogInformation("Launching game via exe...");
                    var storage = viewModel.Model.GetGameStorage(fileSystem, log);
                    var exe = storage.Game.EnumerateFiles().Single(x => x.Name.Equals("rfg.exe", StringComparison.OrdinalIgnoreCase));
                    Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = exe.FullName
                    });
                    break;
                }
            default:
                log.LogInformation("Launching game via Steam...");
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = "steam://rungameid/667720"
                });
                break;
        }

        return Task.FromResult(true);
    }

    internal async Task<bool> RefreshOnline(ViewModel viewModel, CancellationToken token) => await RefreshOnline(viewModel, false, token);

    private async Task<bool> RefreshOnline(ViewModel viewModel, bool isInit, CancellationToken token)
    {
        // always show mods from local directory
        var categories = new List<Category> { Category.Local };
        if (viewModel.Model.DevMode && viewModel.Model.UseCdn)
        {
            log.LogDebug("Listing dev mods from CDN because DevMode and UseCDN are enabled");
            categories.Add(Category.Dev);
        }

        if (viewModel.Model.DevMode && isInit)
        {
            log.LogWarning("Skipped reading mods and news from FactionFiles because DevMode is enabled");
        }
        else
        {
            // add categories to query
            categories.Add(Category.MapPacks);
            categories.Add(Category.MapsStandalone);
            categories.Add(Category.MapsPatches);
            categories.Add(Category.ModsRemaster);

            // upd text
            var document = await ffClient.GetNewsWiki(token);
            var header = document.GetElementById("firstHeading")?.TextContent;
            var content = document.GetElementById("mw-content-text")?.InnerHtml;
            var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
            log.Clear();
            log.LogInformation((Md.NoScroll | Md.H1).Id(), "{header}\n\n", header);
            log.LogInformation((Md.NoScroll | Md.Xaml).Id(), "{xaml}", xaml);
        }

        // upd list
        viewModel.LockCollections(() =>
        {
            viewModel.OnlineMods.Clear();
            viewModel.OnlineSelectedCount = 0;
        });

        return await parallelHelper.Execute(categories, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Fetching online mods", "categories", token);

        async Task Body(Category category, CancellationTokenSource breaker, CancellationToken t)
        {
            var mods = await modLoader.GetMods(category, viewModel.Model.GetGameStorage(fileSystem, log), t);
            viewModel.AddOnlineMods(mods);
        }
    }

    internal async Task<bool> RefreshLocal(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var mods = await modLoader.GetAvailableMods(viewModel.Model.Settings, viewModel.Model.DevMode, storage, token);
        viewModel.UpdateLocalMods(mods);
        return true;
    }

    internal async Task<bool> Update(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem, log);
        var mods = (await modLoader.GetMods(Category.ModsStandalone, gameStorage, token)).ToList();
        var updates = viewModel.LockCollections(() => viewModel.Model.RemoteTerraformUpdates.Concat(viewModel.Model.RemoteRslUpdates).Select(x => mods.Single(y => y.Id == x)).ToList());

        var installed = viewModel.LockCollections(() => viewModel.Model.TerraformUpdates.Concat(viewModel.Model.RslUpdates).ToList());
        var result = await InstallUpdates(installed, updates, gameStorage, viewModel, token);
        if (!result.Success)
        {
            log.LogError("Failed to update game to latest community patch. SyncFaction can't work until you restore all files to their default state.");
            log.LogError("Verify/reinstall your game, then run SyncFaction again.");
            log.LogInformation(Md.I.Id(), "See you later miner!");
            return false;
        }

        viewModel.LockCollections(() =>
        {
            viewModel.Model.TerraformUpdates.Clear();
            viewModel.Model.TerraformUpdates.AddRange(viewModel.Model.RemoteTerraformUpdates);
            viewModel.Model.RslUpdates.Clear();
            viewModel.Model.RslUpdates.AddRange(viewModel.Model.RemoteRslUpdates);
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
        var rsl = await ffClient.ListPatchIds(Constants.RslSearchStringPrefix, token);
        viewModel.UpdateUpdates(terraform, rsl);
    }

    internal void WriteState(ViewModel viewModel)
    {
        if (string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            // nowhere to save state
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

        var mod = viewModel.SelectedMod.Mod;
        viewModel.Model.Settings.Mods.Remove(mod.Id);
    }

    internal async Task<bool> GenerateReport(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, log);
        var fileReports = await fileManager.GenerateFileReport(storage, viewModel.Model.ThreadCount, token);
        var files = fileReports.ToDictionary(static x => x.Path.Replace('\\', '/').PadRight(100), static x => x.ToString());
        var state = viewModel.Model.ToState();
        var report = new Report(files, state, Title.Value, viewModel.LastException);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var logger = (MemoryTarget) LogManager.Configuration.AllTargets.Single(static x => x.Name == "memory");
        var memoryLogs = logger.Logs;
        viewModel.DiagView = json + "\n\n" + string.Join("\n", memoryLogs);
        return true;
    }

    private async Task<ApplyModResult> InstallUpdates(IEnumerable<long> installed, IReadOnlyCollection<IMod> updates, IGameStorage storage, ViewModel viewModel, CancellationToken token)
    {
        var updateIds = updates.Select(static x => x.Id).ToList();
        var filteredUpdateIds = installed.FilterUpdateList(updateIds).ToHashSet();
        var fromScratch = filteredUpdateIds.Count == updateIds.Count;

        var pendingUpdates = updates.Where(x => filteredUpdateIds.Contains(x.Id)).ToList();
        log.LogDebug("Updates to install: [{updates}]", string.Join(", ", pendingUpdates.Select(static x => x.Id)));

        var success = await parallelHelper.Execute(pendingUpdates, Body, viewModel.Model.ThreadCount, TimeSpan.FromSeconds(10), $"Downloading updates", "update", token);
        if (!success)
        {
            return new ApplyModResult(new List<GameFile>(), false);
        }

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

        var installedMods = viewModel.Model.AppliedMods.Select(x => viewModel.LocalMods.First(m => m.Mod.Id == x).Mod).ToList();
        var result = await fileManager.InstallUpdate(storage, pendingUpdates, fromScratch, installedMods, viewModel.Model.IsGog!.Value, viewModel.Model.ThreadCount, token);
        return result;
    }
}
