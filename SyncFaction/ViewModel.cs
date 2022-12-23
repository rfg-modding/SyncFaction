using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HTMLConverter;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;

namespace SyncFaction;

/// <summary>
/// UI-bound app state and commands
/// </summary>
[INotifyPropertyChanged]
public partial class ViewModel
{
    private readonly FfClient ffClient;
    private readonly IFileSystem fileSystem;
    private readonly FileManager fileManager;
    private readonly ILogger<ViewModel> log;
    private readonly IStateProvider stateProvider;
    private readonly AppInitializer appInitializer;

    public ViewModel(FfClient ffClient, ILogger<ViewModel> log, IFileSystem fileSystem, IStateProvider stateProvider, AppInitializer appInitializer, FileManager fileManager) : this()
    {
        this.ffClient = ffClient;
        this.log = log;
        this.stateProvider = stateProvider;
        this.appInitializer = appInitializer;
        this.fileSystem = fileSystem;
        this.fileManager = fileManager;
        OnlineMods.Clear();
        SelectedCount = 0;
    }

    /// <summary>
    /// Default constructor for design time. Initializes data, sets up properties, etc
    /// </summary>
    public ViewModel()
    {
        interactiveCommands = new List<IRelayCommand>()
        {
            FooCommand,
            RefreshCommand,
            RunCommand,
            DownloadCommand,
            CancelCommand
        };


        cancelCommands = new List<ICommand>()
        {
            InitCancelCommand,
            RefreshCancelCommand,
            DownloadCancelCommand
        };

        PropertyChanged += NotifyInteractiveCommands;
        PropertyChanged += UpdateJsonView;
        // TODO callback to log devMode enable/disable

        model = new Model(UpdateJsonView);

        // this allows other threads to work with UI-bound collection
        BindingOperations.EnableCollectionSynchronization(OnlineMods, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.CommunityUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.NewCommunityUpdates, collectionLock);

        // override design-time default to start app without dev panel
        Model.DevMode = true;
        OnlineMods.Add(new ModViewModel(new Mod() {Name = "test_mod_name", Category = Category.Local}, true, _ => { }));
        OnlineMods.Add(new ModViewModel(new Mod() {Name = "selected mod", Category = Category.Local}, false, _ => { }){Selected = true});
        SelectedCount = 1;
    }

    private readonly object collectionLock = new();

    /// <summary>
    /// Trigger lock/unlock for all interactive commands
    /// </summary>
    private void NotifyInteractiveCommands(object? _, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Interactive))
        {
            return;
        }

        foreach (var command in interactiveCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Update json view for display and avoid infinite loop
    /// </summary>
    private void UpdateJsonView(object? _, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(JsonView))
        {
            return;

        }

        OnPropertyChanged(nameof(JsonView));
    }

    [ObservableProperty] private Model model;

    [ObservableProperty] private string currentOperation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotInteractive))]
    private bool interactive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNotRequired))]
    private bool updateRequired = false;

    [ObservableProperty]
    private int selectedCount;

    /// <summary>
    /// For simplified binding
    /// </summary>
    public bool NotInteractive => !interactive;

    /// <summary>
    /// For simplified binding
    /// </summary>
    public bool UpdateNotRequired => !updateRequired;

    /// <summary>
    /// UI-bound collection displayed as ListView
    /// </summary>
    public ObservableCollection<ModViewModel> OnlineMods { get; } = new();

    /// <summary>
    /// Commands to disable while running something
    /// </summary>
    private readonly IReadOnlyList<IRelayCommand> interactiveCommands;

    /// <summary>
    /// Commands to cancel manually
    /// </summary>
    private readonly List<ICommand> cancelCommands;

    /// <summary>
    /// Live debug view of app state
    /// </summary>
    public string JsonView
    {
        get
        {
            try
            {
                var tmp = new
                {
                    Model,
                    CurrentOperation,
                    Interactive,
                    NotInteractive,
                    UpdateRequired,
                    UpdateNotRequired,

                };
                return JsonConvert.SerializeObject(tmp, Formatting.Indented);
            }
            catch (Exception e)
            {
                return e.ToString();
            }

        }
    }

    public IViewAccessor ViewAccessor { get; set; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Init(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock("Initializing", InitInternal, token);

    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private void Foo(object x)
    {
        Interactive = !Interactive;
        FooCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Update(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock("Updating", UpdateInternal, token);

    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Run(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock("Fetching FactionFiles data", RunInternal, token);
    }

    private Task<bool> RunInternal(CancellationToken token)
    {
        if (Model.IsGog)
        {
            log.LogInformation("Launching game via exe...");
            var storage = Model.GetGameStorage(fileSystem);
            var exe = storage.Game.EnumerateFiles().Single(x => x.Name.Equals("rfg.exe", StringComparison.OrdinalIgnoreCase));
            Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = true,
                FileName = exe.FullName
            });
        }
        else
        {
            log.LogInformation("Launching game via Steam...");
            Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = true,
                FileName = "steam://rungameid/667720"
            });
        }

        return Task.FromResult(true);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Download(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock($"Downloading {SelectedCount} mods", DownloadInternal, token);
    }

    [RelayCommand()]
    private async Task Display(object x, CancellationToken token)
    {
        var mvm = (ModViewModel)x;
        await ExecuteAsyncSafe($"Fetching info for {mvm.Mod.Id}", async t => await DisplayInternal(mvm, t), token);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Refresh(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock("Fetching FactionFiles data", RefreshInternal, token);
    }

    [RelayCommand(CanExecute = nameof(NotInteractive))]
    private void Cancel(object x)
    {
        foreach (var command in cancelCommands)
        {
            command.Execute(x);
        }
    }

    [RelayCommand]
    private void Close(object x)
    {
        Cancel(x);
        if (string.IsNullOrWhiteSpace(Model.GameDirectory))
        {
            // nowhere to save state
            return;
        }
        var appStorage = new AppStorage(Model.GameDirectory, fileSystem);
        appStorage.WriteStateFile(Model.SaveToState());
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions. Return true if action succeeded
    /// </summary>
    private async Task ExecuteAsyncSafeWithUiLock(string description, Func<CancellationToken, Task<bool>> action, CancellationToken token)
    {
        if (!Interactive)
        {
            log.LogWarning("Attempt to run command while not intercative, this should not happen normally");
        }

        // disables all clickable controls
        Interactive = false;
        CurrentOperation = description;

        var success = false;
        try
        {
            // TODO what a mess with passing tokens...
            success = await Task.Run(async () => await action(token), token);
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
            // if operation fails, we leave UI disabled
            Interactive = success;
            CurrentOperation = success ? string.Empty : $"FAILED: {CurrentOperation}";
            // TODO: stop spinner and change icon to /!\
        }
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions. Return true if action succeeded
    /// </summary>
    private async Task ExecuteAsyncSafe(string description, Func<CancellationToken, Task<bool>> action, CancellationToken token)
    {
        try
        {
            // TODO what a mess with passing tokens...
            await Task.Run(async () => await action(token), token);
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
            CurrentOperation = string.Empty;
        }
    }

    private async Task<bool> DownloadInternal(CancellationToken token)
    {
        List<ModViewModel> mods;
        lock (collectionLock)
        {
            mods = OnlineMods.Where(x => x.Selected).ToList();
        }

        // sanity check
        if (mods.Count != SelectedCount)
        {
            throw new InvalidOperationException($"Collection length {mods.Count} != SelectedCount {SelectedCount}");
        }

        var toProcess = mods.Count;
        var success = true;

        await Parallel.ForEachAsync(mods, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Model.GetThreadCount()
        }, async (mvm, cancellationToken) =>
        {
            var mod = mvm.Mod;
            var storage = Model.GetGameStorage(fileSystem);
            var modDir = storage.GetModDir(mod);
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, cancellationToken);
            if (!clientSuccess)
            {
                log.LogError("Downloading mod failed");
                success = false;
                return;
            }
            var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
            log.LogDebug($"Mod contents: {files}");
            toProcess--;
            CurrentOperation = $"Downloading {toProcess} mods";
        });

        return success;

        // TODO work with "downloaded" status: initial set and update after DL
        // TODO also detect unpacked and partial state
    }

    private async Task<bool> InitInternal(CancellationToken token)
    {
        if (await appInitializer.Init(Model, token) == false)
        {
            return false;
        }

        var gameStorage = model.GetGameStorage(fileSystem);
        return await InitPopulateData(gameStorage, token);
        // TODO write back state immediately?
    }

    public async Task<bool> InitPopulateData(IGameStorage storage, CancellationToken token)
    {
        // create dirs and validate files if required
        storage.InitBakDirectories();
        var threadCount = Model.GetThreadCount();
        if (Model.IsVerified != true && !await storage.CheckGameFiles(threadCount, log, token))
        {
            return false;
        }

        Model.IsVerified = true;
        if (Model.DevMode)
        {
            log.LogWarning($"Skipped update check because DevMode is enabled");
        }
        else
        {
            // populate community patch info
            await CheckCommunityUpdates(token);
            if (UpdateRequired)
            {
                // we need to update first, don't populate mods
                return true;
            }
        }


        // populate mod list and stuff
        await RefreshInternal(token);

        return true;
    }

    private async Task<bool> RefreshInternal(CancellationToken token)
    {
        var categories = new List<Category>()
        {
            Category.Local
        };
        if (Model.DevMode)
        {
            log.LogWarning($"Skipped reading mods and news from FactionFiles because DevMode is enabled");
        }
        else
        {
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
        lock (collectionLock)
        {
            OnlineMods.Clear();
            SelectedCount = 0;
        }

        await Parallel.ForEachAsync(categories, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Model.GetThreadCount()
        }, async (category, cancellationToken) =>
        {
            var mods = await ffClient.GetMods(category, Model.GetAppStorage(fileSystem), cancellationToken);
            AddModsWithViewResizeOnUiThread(mods);
        });
        return true;
    }

    private async Task<bool> UpdateInternal(CancellationToken token)
    {
        var gameStorage = Model.GetGameStorage(fileSystem);
        var mods = await ffClient.GetMods(Category.ModsStandalone, gameStorage, token);
        var patch = mods.Single(x => x.Id == Model.NewCommunityPatch);
        List<IMod> updates;
        lock (collectionLock)
        {
            updates = Model.NewCommunityUpdates.Select(x => mods.Single(y => y.Id == x)).ToList();
        }

        var result = await InstallUpdates(patch, updates, gameStorage, token);
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

        Model.CommunityPatch = patch.Id;
        lock (collectionLock)
        {
            Model.CommunityUpdates.Clear();
            foreach (var update in updates)
            {
                Model.CommunityUpdates.Add(update.Id);
            }
        }
        gameStorage.WriteStateFile(Model.SaveToState());
        log.LogWarning($"Successfully updated game to community patch: **{Model.GetHumanReadableCommunityVersion(collectionLock)}**");
        return await RefreshInternal(token);
    }

    private async Task<bool> InstallUpdates(IMod patch, List<IMod> updates, GameStorage storage, CancellationToken token)
    {
        if (Model.CommunityPatch != patch.Id)
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

            lock (collectionLock)
            {
                Model.CommunityUpdates.Clear();
            }
        }

        var needPatches = false;
        List<long> installed = null;
        lock (collectionLock)
        {
            needPatches = updates.Select(x => x.Id).SequenceEqual(Model.CommunityUpdates);
            installed = Model.CommunityUpdates.ToList();
        }

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
            log.LogError($"Update community patch failed. please contact developer. `newCommunityVersion=[{Model.NewCommunityPatch}], patch=[{patch.Id}], pending updates count=[{pendingUpdates.Count}]`");
        }

        return result;
    }

    private Task<bool> DisplayInternal(ModViewModel modViewModel, CancellationToken token)
    {
        log.Clear();
        if (modViewModel.Selected)
        {
            log.LogInformation(new EventId(0, "log_false"), modViewModel.Mod.Markdown);
        }

        return Task.FromResult(true);
    }

    public async Task CheckCommunityUpdates(CancellationToken token)
    {
        log.LogInformation($"Installed community patch and updates: **{Model.GetHumanReadableCommunityVersion(collectionLock)}**");
        Model.NewCommunityPatch = await ffClient.GetCommunityPatchId(token);
        var updates = await ffClient.GetCommunityUpdateIds(token);
        lock (collectionLock)
        {
            Model.NewCommunityUpdates.Clear();
            foreach (var u in updates)
            {
                Model.NewCommunityUpdates.Add(u);
            }

            if (Model.CommunityPatch != Model.NewCommunityPatch || !Model.CommunityUpdates.SequenceEqual(Model.NewCommunityUpdates))
            {
                log.LogWarning(@$"You don't have latest community patch installed!

# What is this?

Multiplayer mods depend on community patch and its updates. Even some singleplayer mods too! **It is highly recommended to have latest versions installed.**
This app is designed to keep players updated to avoid issues in multiplayer.
If you don't need this, install mods manually, suggest an improvement at Github or FF Discord, or enable dev mode.

# Press button below to update your game

Mod management will be available after updating.

Changelogs and info:
");
                log.LogInformation($"+ [Community patch base (id {Model.NewCommunityPatch})]({FormatUrl(Model.NewCommunityPatch)})");
                var i = 1;
                foreach (var update in Model.NewCommunityUpdates)
                {
                    log.LogInformation($"+ [Community patch update {i} (id {update})]({FormatUrl(update)})");
                    i++;
                }

                UpdateRequired = true;
            }
            else
            {
                UpdateRequired = false;
            }
        }

        string FormatUrl(long x) => string.Format(Constants.BrowserUrlTemplate, x);
    }

    private void AddModsWithViewResizeOnUiThread(IReadOnlyList<IMod> mods)
    {
        ViewAccessor.OnlineModListView.Dispatcher.Invoke(() =>
        {
            // lock whole batch for less noisy UI updates, inserting category by category
            lock (collectionLock)
            {
                foreach (var mod in mods)
                {
                    var vm = new ModViewModel(mod, false, OnSelectedChanged)
                    {
                        Mod = mod,
                    };
                    OnlineMods.Add(vm);
                }

                var view = ViewAccessor.OnlineModListView.View as GridView;
                if (view == null || view.Columns.Count < 1) return;
                // Simulates column auto sizing as when double-clicking header border
                // it is very important to both insert and update UI sequentially in same thread
                // because otherwise callback for resize can be called before UI had time to update and we will still have wrong column width
                // CollectionChanged event does not help here: it is called after collection change but before UI update
                foreach (var column in view.Columns.Where(x => double.IsNaN(x.Width)))
                {
                    column.Width = column.ActualWidth;
                    column.Width = double.NaN;
                }
            }
        });

    }

    private void OnSelectedChanged(ModViewModel target)
    {
        // ok for AsyncCommand, it's awaited inside Execute()
        DisplayCommand.Execute(target);
        lock (collectionLock)
        {
            SelectedCount = OnlineMods.Count(x => x.Selected);
        }
    }
}

/*
    install mod: fileManager.InstallModExclusive(storage, mod, token);

    restore:
    await Task.Run(async () => { await uiCommands.Restore(gameStorage, true, token); }, token);
    public async Task Restore(IGameStorage storage, bool toVanilla, CancellationToken token)
    {
        log.Clear();
        await fileManager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            // forget we had updates
            stateProvider.State.CommunityPatch = 0;
            stateProvider.State.CommunityUpdates.Clear();
            storage.WriteState(stateProvider.State);
        }
    }
*/
