using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTMLConverter;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;

namespace SyncFaction;

public class UiCommands
{
    private readonly ILogger<UiCommands> log;
    private readonly IFileSystem fileSystem;
    private readonly FfClient ffClient;
    private readonly AppInitializer appInitializer;
    private readonly FileManager fileManager;

    public UiCommands(ILogger<UiCommands> log, IFileSystem fileSystem, FfClient ffClient, AppInitializer appInitializer, FileManager fileManager)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.ffClient = ffClient;
        this.appInitializer = appInitializer;
        this.fileManager = fileManager;
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions. Return true if action succeeded
    /// </summary>
    public async Task ExecuteSafe(ViewModel viewModel, string description, Func<ViewModel, CancellationToken, Task<bool>> action, CancellationToken token, bool lockUi = true)
    {
        if (lockUi && !viewModel.Interactive)
        {
            log.LogWarning("Attempt to run UI-locking command while not intercative, this should not happen normally");
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
            log.LogError(ex, $"TODO better exception logging! \n\n{ex}");
            //var exceptionText = string.Join("\n", ex.ToString().Split('\n').Select(x => $"`` {x} ``"));
            //render.Append("---");
            //render.Append(string.Format(Constants.ErrorFormat, description, exceptionText), false);
        }
        finally
        {
            if (lockUi)
            {
                viewModel.Interactive = true;
            }

            viewModel.CurrentOperation = success ? string.Empty : $"FAILED: {viewModel.CurrentOperation}";
            viewModel.Failure |= !success; // stays forever until restart
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

        var toProcess = mods.Count;
        var success = true;

        await Parallel.ForEachAsync(mods, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = viewModel.Model.GetThreadCount()
        }, async (mvm, cancellationToken) =>
        {
            // display "in progress" regardless of real mod status
            mvm.Status = OnlineModStatus.InProgress;
            var mod = mvm.Mod;
            var storage = viewModel.Model.GetGameStorage(fileSystem);
            var modDir = storage.GetModDir(mod);
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, cancellationToken);
            mvm.Status = mod.Status; // status is changed by from ffClient
            if (!clientSuccess)
            {
                log.LogError("Downloading mod failed");
                success = false;
                return;
            }

            var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
            log.LogDebug($"Mod contents: {files}");
            toProcess--;
            viewModel.CurrentOperation = $"Downloading {toProcess} mods";
        });
        if (success == false)
        {
            return false;
        }

        return await RefreshLocal(viewModel, token);
    }

    public async Task<bool> Apply(ViewModel viewModel, CancellationToken token)
    {
        throw new NotImplementedException();
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
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem);
        // create dirs and validate files if required
        gameStorage.InitBakDirectories();
        var threadCount = viewModel.Model.GetThreadCount();
        if (viewModel.Model.IsVerified != true && !await gameStorage.CheckGameFiles(threadCount, log, token))
        {
            return false;
        }

        viewModel.Model.IsVerified = true;
        if (viewModel.Model.DevMode)
        {
            log.LogWarning($"Skipped update check because DevMode is enabled");
        }
        else
        {
            // populate community patch info
            await CheckCommunityUpdates(viewModel, token);
            if (viewModel.UpdateRequired)
            {
                // we need to update first, don't populate mods
                return true;
            }
        }


        // populate mod list and stuff
        await RefreshLocal(viewModel, token);
        await RefreshOnline(viewModel, token);

        return true;
    }

    public async Task<bool> RefreshLocal(ViewModel viewModel, CancellationToken token)
    {
        var storage = viewModel.Model.GetAppStorage(fileSystem);
        var mods = await GetAvailableMods(storage, token);
        viewModel.UpdateLocalMods(mods);
        return true;
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
                var storage = viewModel.Model.GetGameStorage(fileSystem);
                var exe = storage.Game.EnumerateFiles().Single(x => x.Name.Equals("rfg.exe", StringComparison.OrdinalIgnoreCase));
                Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = exe.FullName
                });
                break;
            }
            default:
                log.LogInformation("Launching game via Steam...");
                Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "steam://rungameid/667720"
                });
                break;
        }

        return Task.FromResult(true);
    }

    private async Task<List<IMod>> GetAvailableMods(IAppStorage storage, CancellationToken token)
    {
        // TODO read mod flags
        List<IMod> mods = new();
        foreach (var dir in storage.App.EnumerateDirectories())
        {
            if (dir.Name.StartsWith("."))
            {
                // skip unix-hidden files
                continue;
            }

            if (dir.Name.StartsWith("Mod_") && dir.Name.Substring(4).All(char.IsDigit))
            {
                // read mod description from json
                var descriptionFile = fileSystem.FileInfo.FromFileName(Path.Combine(dir.FullName, Constants.ModDescriptionFile));
                if (!descriptionFile.Exists)
                {
                    // mod was not downloaded correctly
                    continue;
                }

                using (var reader = descriptionFile.OpenText())
                {
                    var json = await reader.ReadToEndAsync();
                    var modFromJson = JsonConvert.DeserializeObject<Mod>(json);
                    mods.Add(modFromJson);
                }
            }
            else
            {
                var mod = new LocalMod()
                {
                    Id = dir.Name.GetHashCode(),
                    Name = dir.Name,
                    Size = 0,
                    DownloadUrl = null,
                    ImageUrl = null,
                    Status = OnlineModStatus.Ready
                };
                mods.Add(mod);
            }
        }

        return mods;
    }

    public async Task<bool> RefreshOnline(ViewModel viewModel, CancellationToken token)
    {
        // always show mods from local directory
        var categories = new List<Category>()
        {
            Category.Local
        };
        if (viewModel.Model.DevMode)
        {
            log.LogWarning($"Skipped reading mods and news from FactionFiles because DevMode is enabled");
        }
        else
        {
            // add categories to query
            categories.Add(Category.MapPacks);
            categories.Add(Category.MapsStandalone);
            categories.Add(Category.MapsPatches);

            // upd text
            var document = await ffClient.GetNewsWiki(token);
            var header = document.GetElementById("firstHeading").TextContent;
            var content = document.GetElementById("mw-content-text").InnerHtml;
            var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
            log.Clear();
            log.LogInformation(new EventId(0, "log_false"), $"# {header}\n\n");
            log.LogInformationXaml(xaml, false);
        }

        // upd list
        viewModel.LockedCollectionOperation(() =>
        {
            viewModel.OnlineMods.Clear();
            viewModel.OnlineSelectedCount = 0;
        });

        await Parallel.ForEachAsync(categories, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = viewModel.Model.GetThreadCount()
        }, async (category, cancellationToken) =>
        {
            var mods = await ffClient.GetMods(category, viewModel.Model.GetGameStorage(fileSystem), cancellationToken);
            viewModel.AddModsWithViewResizeOnUiThread(mods);
        });
        return true;


        // TODO work with "downloaded" status: initial set and update after DL
        // TODO also detect unpacked and partial state
    }

    public async Task<bool> Update(ViewModel viewModel, CancellationToken token)
    {
        var gameStorage = viewModel.Model.GetGameStorage(fileSystem);
        var mods = await ffClient.GetMods(Category.ModsStandalone, gameStorage, token);
        var patch = mods.Single(x => x.Id == viewModel.Model.NewCommunityPatch);
        var updates = viewModel.LockedCollectionOperation(() => viewModel.Model.NewCommunityUpdates.Select(x => mods.Single(y => y.Id == x)).ToList());
        var result = await InstallUpdates(viewModel, patch, updates, gameStorage, token);
        if (!result)
        {
            log.LogError(@$"Action needed:

Failed to update game to latest community patch.

SyncFaction can't work until you restore all files to their default state.

+ **Steam**: verify integrity of game files and let it download original data
+ **GOG**: reinstall game

Then run SyncFaction again.

*See you later miner!*
");
            return false;
        }

        viewModel.Model.CommunityPatch = patch.Id;
        viewModel.LockedCollectionOperation(() =>
        {
            viewModel.Model.CommunityUpdates.Clear();
            foreach (var update in updates)
            {
                viewModel.Model.CommunityUpdates.Add(update.Id);
            }
        });
        gameStorage.WriteStateFile(viewModel.Model.ToState());
        log.LogWarning($"Successfully updated game to community patch: **{viewModel.GetHumanReadableCommunityVersion()}**");
        viewModel.UpdateRequired = false;
        return await RefreshOnline(viewModel, token);
    }

    private async Task<bool> InstallUpdates(ViewModel viewModel, IMod patch, List<IMod> updates, GameStorage storage, CancellationToken token)
    {
        if (viewModel.Model.CommunityPatch != patch.Id)
        {
            var modDir = storage.GetModDir(patch);
            var successDl = await ffClient.DownloadAndUnpackMod(modDir, patch, token);
            if (!successDl)
            {
                return false;
            }

            var successPatch = await fileManager.InstallCommunityPatchBase(storage, patch, token);
            if (!successPatch)
            {
                return false;
            }

            viewModel.LockedCollectionOperation(() => { viewModel.Model.CommunityUpdates.Clear(); });
        }

        var needPatches = false;
        List<long> installed = null;
        viewModel.LockedCollectionOperation(() =>
        {
            needPatches = updates.Select(x => x.Id).SequenceEqual(viewModel.Model.CommunityUpdates);
            installed = viewModel.Model.CommunityUpdates.ToList();
        });

        if (!needPatches)
        {
            return true;
        }

        var pendingUpdates = updates.ToList();
        while (installed.Any())
        {
            var current = installed.First();
            var apiUpdate = pendingUpdates.First();
            if (current != apiUpdate.Id)
            {
                log.LogError($"Updates are mixed up, please contact developer");
                return false;
            }

            installed.RemoveAt(0);
            pendingUpdates.RemoveAt(0);
        }

        log.LogDebug($"Updates to install: {JsonConvert.SerializeObject(pendingUpdates)}");
        foreach (var update in pendingUpdates)
        {
            var updDir = storage.GetModDir(update);
            var success = await ffClient.DownloadAndUnpackMod(updDir, update, token);
            if (!success)
            {
                return false;
            }
        }

        var result = await fileManager.InstallCommunityUpdateIncremental(storage, pendingUpdates, token);
        if (!result)
        {
            log.LogError($"Update community patch failed. please contact developer. `newCommunityVersion=[{viewModel.Model.NewCommunityPatch}], patch=[{patch.Id}], pending updates count=[{pendingUpdates.Count}]`");
        }

        return result;
    }

    public async Task CheckCommunityUpdates(ViewModel viewModel, CancellationToken token)
    {
        log.LogInformation($"Installed community patch and updates: **{viewModel.GetHumanReadableCommunityVersion()}**");
        var newPatch = await ffClient.GetCommunityPatchId(token);
        var updates = await ffClient.GetCommunityUpdateIds(token);
        viewModel.UpdateUpdates(newPatch, updates);
    }
}