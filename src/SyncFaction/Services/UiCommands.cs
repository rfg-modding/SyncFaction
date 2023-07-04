using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastHashes;
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
using SyncFaction.ModManager;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.XmlModels;
using SyncFaction.ViewModels;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SyncFaction.Services;

public class UiCommands
{
    private readonly ILogger<UiCommands> log;
    private readonly IFileSystem fileSystem;
    private readonly FfClient ffClient;
    private readonly AppInitializer appInitializer;
    private readonly FileManager fileManager;
    private readonly ModTools modTools;
    private readonly ParallelHelper parallelHelper;

    public UiCommands(IFileSystem fileSystem, FfClient ffClient, AppInitializer appInitializer, FileManager fileManager, ModTools modTools, ParallelHelper parallelHelper, ILogger<UiCommands> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.ffClient = ffClient;
        this.appInitializer = appInitializer;
        this.fileManager = fileManager;
        this.modTools = modTools;
        this.parallelHelper = parallelHelper;
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions. Return true if action succeeded
    /// </summary>
    public async Task ExecuteSafe(ViewModel viewModel, string description, Func<ViewModel, CancellationToken, Task<bool>> action, CancellationToken token, bool lockUi = true)
    {
        if (lockUi && !viewModel.Interactive)
        {
            log.LogWarning("Attempt to run UI-locking command while not interactive, this should not happen normally");
        }

        if (lockUi)
        {
            // disables all clickable controls
            viewModel.Interactive = false;
        }

        viewModel.CurrentOperation = description;
        var success = false;
        try
        {
            // TODO what a mess with passing tokens...
            success = await Task.Run(async () => await action(viewModel, token), token);
        }
        catch (Exception ex)
        {
            viewModel.LastException = ex.ToString();
            log.LogError(Md.H1.ToId(), "FAILED: {operation}", viewModel.CurrentOperation);
            log.LogError("Things to try now:");
            log.LogError(Md.Bullet.ToId(), "Carefully read log above and error message below");
            log.LogError(Md.Bullet.ToId(), "Verify your game and try again");
            log.LogError(Md.Bullet.ToId(), "Generate diagnostics report and ask for help (FF Discord or Github)");
            log.LogError(ex, "Exception details:");
        }
        finally
        {
            if (lockUi)
            {
                viewModel.Interactive = true;
            }

            viewModel.CurrentOperation = success
                ? string.Empty
                : $"FAILED: {viewModel.CurrentOperation}";
            viewModel.GeneralFailure |= !success; // stays forever until restart
        }
    }

    public async Task<bool> Download(ViewModel viewModel, CancellationToken token)
    {
        var mods = viewModel.LockedCollectionOperation(() => viewModel.OnlineMods.Where(x => x.Selected).ToList());

        // sanity check
        if (mods.Count != viewModel.OnlineSelectedCount)
        {
            throw new InvalidOperationException($"Collection length {mods.Count} != SelectedCount {viewModel.OnlineSelectedCount}");
        }

        var threadCount = viewModel.Model.CalculateThreadCount();
        await parallelHelper.Execute(mods, Body, threadCount, TimeSpan.FromSeconds(10), $"Downloading {mods.Count} mods", "mods", token);
        return await RefreshLocal(viewModel, token);

        async Task Body(OnlineModViewModel mvm, CancellationTokenSource breaker, CancellationToken t)
        {
            // display "in progress" regardless of real mod status
            mvm.Status = OnlineModStatus.InProgress;
            var mod = mvm.Mod;
            var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
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

    public async Task<bool> Apply(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, false, token);
        var modsToApply = viewModel.LockedCollectionOperation(() => viewModel.LocalMods.Where(x => x.Status == LocalModStatus.Enabled).ToList());
        foreach (var mvm in modsToApply)
        {
            if (mvm.Mod.ModInfo is not null)
            {
                viewModel.Model.Settings.Mods[mvm.Mod.Id] = modTools.SaveCurrentSettings(mvm.Mod.ModInfo);
                modTools.ApplyUserInput(mvm.Mod.ModInfo);
            }
        }

        var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        foreach (var vm in modsToApply)
        {
            var result = await fileManager.InstallMod(storage, vm.Mod, viewModel.Model.IsGog!.Value, false, token);

            if (!result.Success)
            {
                return false;
            }
        }

        viewModel.LockedCollectionOperation(() =>
        {
            foreach (var vm in modsToApply)
            {
                viewModel.Model.AppliedMods.Add(vm.Mod.Id);
            }
        });
        return true;
    }

    public async Task<bool> Display(ViewModel viewModel, CancellationToken token)
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

    public async Task<bool> RestoreMods(ViewModel viewModel, CancellationToken token)
    {
        // TODO
        throw new NotImplementedException();
    }

    public async Task<bool> RestorePatch(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, false, token);
        return true;
    }

    public async Task<bool> RestoreVanilla(ViewModel viewModel, CancellationToken token)
    {
        await RestoreInternal(viewModel, true, token);
        return true;
    }

    public async Task RestoreInternal(ViewModel viewModel, bool toVanilla, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        fileManager.Rollback(storage, toVanilla, token);
        viewModel.Model.AppliedMods.Clear();
        if (toVanilla)
        {
            viewModel.LockedCollectionOperation(() =>
            {
                // forget we had updates entirely
                viewModel.Model.TerraformUpdates.Clear();
                viewModel.Model.RslUpdates.Clear();
            });
        }
    }

    public async Task<bool> Init(ViewModel viewModel, CancellationToken token)
    {
        if (await appInitializer.Init(viewModel, token) == false)
        {
            return false;
        }

        return await InitPopulateData(viewModel, token);
        // TODO write back state immediately?
    }

    public async Task<bool> InitPopulateData(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        // create dirs and validate files if required
        gameStorage.InitBakDirectories();
        var threadCount = viewModel.Model.CalculateThreadCount();
        if (viewModel.Model.IsVerified != true && !await gameStorage.CheckGameFiles(threadCount, log, token))
        {
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

    public Task<bool> Run(ViewModel viewModel, CancellationToken token)
    {
        switch (viewModel.Model.IsGog)
        {
            case null:
                throw new InvalidOperationException("App is not properly initialized, still don't know game version");
            case true:
                {
                    log.LogInformation("Launching game via exe...");
                    var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
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

    public async Task<bool> RefreshOnline(ViewModel viewModel, CancellationToken token) => await RefreshOnline(viewModel, false, token);

    public async Task<bool> RefreshOnline(ViewModel viewModel, bool isInit, CancellationToken token)
    {
        // always show mods from local directory
        var categories = new List<Category> { Category.Local };
        if (viewModel.Model.DevMode && viewModel.Model.UseCdn)
        {
            log.LogDebug("Listing dev mods from CDN because DevMode and UseCDN is enabled");
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
            var header = document.GetElementById("firstHeading").TextContent;
            var content = document.GetElementById("mw-content-text").InnerHtml;
            var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
            log.Clear();
            //log.LogInformation(new EventId(0, "log_false"), $"# {header}\n\n");
            log.LogInformation(Md.Xaml.ToId(), "{xaml}", xaml);
        }

        // upd list
        viewModel.LockedCollectionOperation(() =>
        {
            viewModel.OnlineMods.Clear();
            viewModel.OnlineSelectedCount = 0;
        });

        var threadCount = viewModel.Model.CalculateThreadCount();
        return await parallelHelper.Execute(categories, Body, threadCount, TimeSpan.FromSeconds(10), $"Fetching {categories.Count} categories", "categories", token);

        async Task Body(Category category, CancellationTokenSource breaker, CancellationToken t)
        {
            var mods = await ffClient.GetMods(category, viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log), t);
            viewModel.AddOnlineMods(mods);
        }
    }

    public async Task<bool> RefreshLocal(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        var mods = await GetAvailableMods(viewModel, storage, token);
        viewModel.UpdateLocalMods(mods);
        return true;
    }

    public async Task<bool> Update(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        // TODO remove me!
        var mods = (await ffClient.GetMods(Category.ModsStandalone, gameStorage, token)).Concat(await ffClient.GetMods(Category.Local, gameStorage, token)).Concat(await ffClient.GetMods(Category.Dev, gameStorage, token)).ToList();
        var updates = viewModel.LockedCollectionOperation(() => viewModel.Model.RemoteTerraformUpdates.Concat(viewModel.Model.RemoteRslUpdates).Select(x => mods.Single(y => y.Id == x)).ToList());
        // patches are not intended to be installed locally, will store them as hidden from local mod list
        foreach (var x in updates)
        {
            x.Hide = true;
        }

        var installed = viewModel.LockedCollectionOperation(() => viewModel.Model.TerraformUpdates.Concat(viewModel.Model.RslUpdates).ToList());
        var result = await InstallUpdates(installed, updates, gameStorage, viewModel, token);
        if (!result.Success)
        {
            log.LogError(@"Action needed:

Failed to update game to latest community patch.

SyncFaction can't work until you restore all files to their default state.

+ **Steam**: verify integrity of game files and let it download original data
+ **GOG**: reinstall game

Then run SyncFaction again.

See you later miner!
");
            return false;
        }

        viewModel.LockedCollectionOperation(() =>
        {
            viewModel.Model.TerraformUpdates.Clear();
            viewModel.Model.TerraformUpdates.AddRange(viewModel.Model.RemoteTerraformUpdates);
            viewModel.Model.RslUpdates.Clear();
            viewModel.Model.RslUpdates.AddRange(viewModel.Model.RemoteRslUpdates);
        });
        gameStorage.WriteStateFile(viewModel.Model.ToState());
        //log.LogWarning($"Successfully updated game: **{viewModel.GetHumanReadableVersion()}**");
        viewModel.UpdateRequired = false;

        // populate mod list and stuff
        return await RefreshLocal(viewModel, token) && await RefreshOnline(viewModel, token);
    }

    public async Task CheckPatchUpdates(ViewModel viewModel, CancellationToken token)
    {
        log.LogInformation("Installed community patch and updates: **{installer}**", viewModel.GetHumanReadableVersion());
        var terraform = await ffClient.ListPatchIds(Constants.PatchSearchStringPrefix, token);
        var rsl = await ffClient.ListPatchIds(Constants.RslSearchStringPrefix, token);
        viewModel.UpdateUpdates(terraform, rsl);
    }

    public void WriteState(ViewModel viewModel)
    {
        if (string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            // nowhere to save state
            return;
        }

        var appStorage = new AppStorage(viewModel.Model.GameDirectory, fileSystem, parallelHelper, log);
        appStorage.WriteStateFile(viewModel.Model.ToState());
    }

    public void ModResetInputs(ModInfo modInfo, ViewModel viewModel)
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

    public async Task<bool> GenerateReport(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetGameStorage(fileSystem, parallelHelper, log);
        var fileReports = await fileManager.GenerateFileReport(storage, viewModel.Model.ThreadCount, token);
        var files = fileReports.ToDictionary(x => x.Path.Replace('\\', '/').PadRight(100), x => x.ToString());
        var state = viewModel.Model.ToState();
        var report = new Report(files, state, Title.Value, viewModel.LastException);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var logger = (MemoryTarget) LogManager.Configuration.AllTargets.Single(x => x.Name == "memory");
        var memoryLogs = logger.Logs;
        viewModel.DiagView = json + "\n\n" + string.Join("\n", memoryLogs);
        return true;
    }

    private async Task<List<IMod>> GetAvailableMods(ViewModel viewModel, IGameStorage storage, CancellationToken token)
    {
        var result = new List<IMod>();
        foreach (var mod in EnumerateModFolders(storage, viewModel.Model.DevMode))
        {
            SetFlags(mod, storage);
            mod.ModInfo = await ReadModInfo(mod, viewModel.Model.Settings, storage, token);
            result.Add(mod);
        }

        return result;
    }

    private async Task<ModInfo?> ReadModInfo(IMod mod, Settings settings, IGameStorage storage, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        var xmlFile = modDir.EnumerateFiles("modinfo.xml", SearchOption.AllDirectories).FirstOrDefault();
        if (xmlFile is null)
        {
            return null;
        }

        token.ThrowIfCancellationRequested();
        await using var s = xmlFile.OpenRead();
        var modInfo = modTools.LoadFromXml(s, xmlFile.Directory);
        modTools.CopySameOptions(modInfo);
        if (settings.Mods.TryGetValue(mod.Id, out var modSettings))
        {
            modTools.LoadSettings(modSettings, modInfo);
        }

        return modInfo;
    }

    private IEnumerable<IMod> EnumerateModFolders(IGameStorage storage, bool devMode)
    {
        // TODO make async enumerable for ReadAsync()?
        foreach (var dir in storage.App.EnumerateDirectories())
        {
            if (dir.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                // skip unix-hidden files
                continue;
            }

            if (dir.Name.StartsWith("Mod_", StringComparison.OrdinalIgnoreCase))
            {
                // read mod description from json
                var descriptionFile = fileSystem.FileInfo.New(Path.Combine(dir.FullName, Constants.ModDescriptionFile));
                if (!descriptionFile.Exists)
                {
                    // mod was not downloaded correctly
                    continue;
                }

                using (var reader = descriptionFile.OpenText())
                {
                    var json = reader.ReadToEnd();
                    var mod = JsonSerializer.Deserialize<Mod>(json);
                    if (mod.Hide && !devMode)
                    {
                        continue;
                    }

                    yield return mod;
                }
            }
            else
            {
                var id = BitConverter.ToInt64(new MurmurHash64().ComputeHash(Encoding.UTF8.GetBytes(dir.Name.ToLowerInvariant())));
                var mod = new LocalMod
                {
                    Id = id,
                    Name = dir.Name,
                    Size = 0,
                    DownloadUrl = string.Empty,
                    ImageUrl = null,
                    Status = OnlineModStatus.Ready
                };
                yield return mod;
            }
        }
    }

    private void SetFlags(IMod mod, IGameStorage storage)
    {
        var modDir = storage.GetModDir(mod);
        var modFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.StartsWith(".mod", StringComparison.OrdinalIgnoreCase));
        var flags = ModFlags.None;

        foreach (var modFile in modFiles)
        {
            var extension = modFile.Extension.ToLowerInvariant();
            var name = modFile.Name.ToLowerInvariant();

            if (!modFile.IsModContent())
            {
                continue;
            }

            if (extension is ".xdelta")
            {
                flags |= ModFlags.HasXDelta;
            }
            else if (name is "modinfo.xml")
            {
                flags |= ModFlags.HasModInfo;
            }
            else
            {
                flags |= ModFlags.HasReplacementFiles;
            }

            // detecting if there are mp_file.vpp or mp_file.xdelta
            var nameNoExt = Path.GetFileNameWithoutExtension(name) + ".";
            if (Hashes.MultiplayerFiles.Any(x => x.StartsWith(nameNoExt, StringComparison.OrdinalIgnoreCase)))
            {
                // TODO: read modinfo.xml because it can affect MP files too
                flags |= ModFlags.AffectsMultiplayerFiles;
            }

            mod.Flags = flags;
        }
    }

    private async Task<ApplyModResult> InstallUpdates(List<long> installed, List<IMod> updates, GameStorage storage, ViewModel viewModel, CancellationToken token)
    {
        var updateIds = updates.Select(x => x.Id).ToList();
        var filteredUpdateIds = installed.FilterUpdateList(updateIds).ToHashSet();
        var fromScratch = filteredUpdateIds.Count == updateIds.Count;

        var pendingUpdates = updates.Where(x => filteredUpdateIds.Contains(x.Id)).ToList();
        //log.LogDebug($"Updates to install: {JsonConvert.SerializeObject(pendingUpdates)}");

        var threadCount = viewModel.Model.CalculateThreadCount();
        var success = await parallelHelper.Execute(pendingUpdates, Body, threadCount, TimeSpan.FromSeconds(10), $"Downloading {pendingUpdates.Count} updates", "update", token);
        if (!success)
        {
            return new ApplyModResult(new List<GameFile>(), false);
        }

        async Task Body(IMod mod, CancellationTokenSource breaker, CancellationToken t)
        {
            var modDir = storage.GetModDir(mod);
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, t);
            if (!clientSuccess)
            {
                log.LogError("Downloading update failed");
                success = false;
                return;
            }

            var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
            //log.LogDebug($"Update contents: {files}");
        }

        var installedMods = viewModel.Model.AppliedMods.Select(x => viewModel.LocalMods.First(m => m.Mod.Id == x).Mod).ToList();
        var result = await fileManager.InstallUpdate(storage, pendingUpdates, fromScratch, installedMods, viewModel.Model.IsGog!.Value, token);
        return result;
    }
}
