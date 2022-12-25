using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
/// UI-bound commands
/// </summary>
[INotifyPropertyChanged]
public partial class ViewModelCommands
{
    private readonly ViewModel viewModel;

    // TODO move stuff here
    public ViewModelCommands(ViewModel viewModel)
    {
        this.viewModel = viewModel;
    }
}

/// <summary>
/// UI-bound app state
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

        SetDesignTimeDefaults(false);
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
            ApplyCommand,
            CancelCommand
        };


        cancelCommands = new List<ICommand>()
        {
            InitCancelCommand,
            RefreshCancelCommand,
            DownloadCancelCommand,
            ApplyCancelCommand
        };

        // TODO callback to log devMode enable/disable
        PropertyChanged += NotifyInteractiveCommands;
        PropertyChanged += UpdateJsonView;
        model = new Model();
        model.PropertyChanged += UpdateJsonView;
        LocalMods.CollectionChanged += LocalModsOnCollectionChanged;

        // this allows other threads to work with UI-bound collection
        BindingOperations.EnableCollectionSynchronization(OnlineMods, collectionLock);
        BindingOperations.EnableCollectionSynchronization(LocalMods, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.CommunityUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.NewCommunityUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.AppliedMods, collectionLock);

        SetDesignTimeDefaults(true);
    }

    private void LocalModsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LocalModCalculateOrder();
    }

    private void SetDesignTimeDefaults(bool isDesignTime)
    {
        if (isDesignTime)
        {
            // design-time defaults
            Model.DevMode = true;
            OnlineSelectedCount = 1;
            LocalSelectedCount = 2;
            GridLines = true;
            SelectedTab = Tab.Apply;

            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "just a mod", Category = Category.Local}));
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "selected mod", Category = Category.Local}) {Selected = true});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "ready (dl+unp) mod", Category = Category.Local}) {Status = OnlineModStatus.Ready});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "mod in progress", Category = Category.Local}) {Status = OnlineModStatus.InProgress});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "failed mod", Category = Category.Local}) {Status = OnlineModStatus.Failed});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "mod with a ridiculously long name so nobody will read it entirely unless they really want to", Category = Category.Local}));

            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "just a mod"}));
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "selected mod"}) {Selected = true});
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "mod 3"}) {Order = 3});
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "mod 1"}) {Order = 1});
            foreach (var localMod in LocalMods)
            {
                localMod.PropertyChanged += LocalModOnPropertyChanged;
            }
        }
        else
        {
            Model.DevMode = false;
            OnlineMods.Clear();
            LocalMods.Clear();
            OnlineSelectedCount = 0;
            LocalSelectedCount = 0;
            Failure = false;
            GridLines = false;
            SelectedTab = Tab.Apply;
        }
    }

    private void LocalModOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalModViewModel.Order) or nameof(LocalModViewModel.Selected))
        {
            return;
        }
        LocalModCalculateOrder();
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

    public GroupedDropHandler DropHandler { get; } = new GroupedDropHandler();

    public static readonly SolidColorBrush Highlight = new((Color)ColorConverter.ConvertFromString("#F59408"));

    [ObservableProperty] private Model model;

    [ObservableProperty] private string currentOperation = string.Empty;

    // TODO save to state to remember last opened tab
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModSettings))]
    private Tab selectedTab = Tab.Apply;

    // TODO show this only when mod has modinfo.xml with inputs?
    public bool DisplayModSettings => SelectedTab == Tab.Apply;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotInteractive))]
    private bool interactive = true;

    [ObservableProperty]
    private bool failure = true;

    [ObservableProperty]
    private bool gridLines;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNotRequired))]
    private bool updateRequired = false;

    [ObservableProperty]
    private int onlineSelectedCount;

    [ObservableProperty]
    private int localSelectedCount;

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
    public ObservableCollection<OnlineModViewModel> OnlineMods { get; } = new();

    public ObservableCollection<LocalModViewModel> LocalMods { get; } = new();

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
                    OnlineSelectedCount,
                    SelectedTab,
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

    [RelayCommand]
    private async Task Test(object x, CancellationToken token)
    {
        LocalModCalculateOrder();
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

    [RelayCommand]
    private async Task OpenDir(object x, CancellationToken token)
    {
        var arg = x as string ?? string.Empty;
        var destination = Path.Combine(Model.GameDirectory, arg);
        Process.Start(new ProcessStartInfo()
        {
            UseShellExecute = true,
            FileName = destination
        });
    }

    private Task<bool> RunInternal(CancellationToken token)
    {
        switch (Model.IsGog)
        {
            case null:
                throw new InvalidOperationException("App is not properly initialized, still don't know game version");
            case true:
            {
                log.LogInformation("Launching game via exe...");
                var storage = Model.GetGameStorage(fileSystem);
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

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Download(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock($"Downloading {OnlineSelectedCount} mods", DownloadInternal, token);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Apply(object x, CancellationToken token)
    {
        await ExecuteAsyncSafeWithUiLock($"Applying {-42} mods", ApplyInternal, token);
    }

    [RelayCommand()]
    private async Task Display(object x, CancellationToken token)
    {
        var mvm = (IModViewModel)x;
        await ExecuteAsyncSafeWithUiLock($"Fetching info for {mvm.Mod.Id}", async t => await DisplayInternal(mvm, t), token, lockUi:false);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Refresh(object x, CancellationToken token)
    {
        switch (SelectedTab)
        {
            case Tab.Download:
                await ExecuteAsyncSafeWithUiLock("Fetching FactionFiles data", RefreshOnline, token);
                break;
            case Tab.Apply:
                await ExecuteAsyncSafeWithUiLock("Looking for mods", RefreshLocal, token);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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
        appStorage.WriteStateFile(Model.ToState());
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions. Return true if action succeeded
    /// </summary>
    private async Task ExecuteAsyncSafeWithUiLock(string description, Func<CancellationToken, Task<bool>> action, CancellationToken token, bool lockUi=true)
    {
        if (lockUi && !Interactive)
        {
            log.LogWarning("Attempt to run UI-locking command while not intercative, this should not happen normally");
        }

        if (lockUi)
        {

            // disables all clickable controls
            Interactive = false;
        }

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
            if (lockUi)
            {
                Interactive = true;
            }

            CurrentOperation = success ? string.Empty : $"FAILED: {CurrentOperation}";
            Failure |= !success;  // stays forever until restart
        }
    }

    private async Task<bool> DownloadInternal(CancellationToken token)
    {
        List<OnlineModViewModel> mods;
        lock (collectionLock)
        {
            mods = OnlineMods.Where(x => x.Selected).ToList();
        }

        // sanity check
        if (mods.Count != OnlineSelectedCount)
        {
            throw new InvalidOperationException($"Collection length {mods.Count} != SelectedCount {OnlineSelectedCount}");
        }

        var toProcess = mods.Count;
        var success = true;

        await Parallel.ForEachAsync(mods, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Model.GetThreadCount()
        }, async (mvm, cancellationToken) =>
        {
            // display "in progress" regardless of real mod status
            mvm.Status = OnlineModStatus.InProgress;
            var mod = mvm.Mod;
            var storage = Model.GetGameStorage(fileSystem);
            var modDir = storage.GetModDir(mod);
            var clientSuccess = await ffClient.DownloadAndUnpackMod(modDir, mod, cancellationToken);
            mvm.Status = mod.Status;  // status is changed by from ffClient
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
        if (success == false)
        {
            return false;
        }
        return await RefreshLocal(token);
    }

    private async Task<bool> ApplyInternal(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    private async Task<bool> InitInternal(CancellationToken token)
    {
        if (await appInitializer.Init(Model, ViewAccessor, token) == false)
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
        await RefreshLocal(token);
        await RefreshOnline(token);

        return true;
    }

    private async Task<bool> RefreshLocal(CancellationToken token)
    {
        var storage = Model.GetAppStorage(fileSystem);
        var mods = await GetAvailableMods(storage, token);
        // TODO compare applied mods from state with current mod
        ViewAccessor.LocalModListView.Dispatcher.Invoke(() =>
        {
            lock (collectionLock)
            {
                LocalMods.Clear();
                foreach (var mod in mods)
                {
                    var vm = new LocalModViewModel(mod);
                    vm.PropertyChanged += LocalModOnPropertyChanged;
                    vm.PropertyChanged += LocalModDisplay;
                    LocalMods.Add(vm);
                }
            }
        });


        return true;
    }

    private async Task<List<IMod>> GetAvailableMods(IAppStorage storage, CancellationToken token)
    {
        // TODO read mod flags
        List<IMod> mods = new ();
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

    private async Task<bool> RefreshOnline(CancellationToken token)
    {
        // always show mods from local directory
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
        lock (collectionLock)
        {
            OnlineMods.Clear();
            OnlineSelectedCount = 0;
        }

        await Parallel.ForEachAsync(categories, new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Model.GetThreadCount()
        }, async (category, cancellationToken) =>
        {
            var mods = await ffClient.GetMods(category, Model.GetGameStorage(fileSystem), cancellationToken);
            AddModsWithViewResizeOnUiThread(mods);
        });
        return true;


        // TODO work with "downloaded" status: initial set and update after DL
        // TODO also detect unpacked and partial state
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
        gameStorage.WriteStateFile(Model.ToState());
        log.LogWarning($"Successfully updated game to community patch: **{Model.GetHumanReadableCommunityVersion(collectionLock)}**");
        return await RefreshOnline(token);
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

    private Task<bool> DisplayInternal(IModViewModel modViewModel, CancellationToken token)
    {
        if (modViewModel.Selected)
        {
            log.Clear();
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
                    var vm = new OnlineModViewModel(mod)
                    {
                        Mod = mod,
                    };
                    vm.PropertyChanged += OnlilneModDisplayAndUpdateCount;
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

    private void OnlilneModDisplayAndUpdateCount(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnlineModViewModel.Selected))
        {
            return;
        }

        var target = sender as OnlineModViewModel;
        // it's AsyncCommand, ok to call this way: awaited inside Execute()
        DisplayCommand.Execute(target);
        lock (collectionLock)
        {
            OnlineSelectedCount = OnlineMods.Count(x => x.Selected);
        }
    }

    private void LocalModDisplay(object? sender, PropertyChangedEventArgs e)
    {
        // TODO: gets called twice for some reason
        // TODO: unable to deselect items, why?!
        if (e.PropertyName != nameof(LocalModViewModel.Selected))
        {
            return;
        }

        var target = sender as LocalModViewModel;
        // it's AsyncCommand, ok to call this way: awaited inside Execute()
        DisplayCommand.Execute(target);
    }

    private void LocalModCalculateOrder()
    {

        lock (collectionLock)
        {

            var enabled = LocalMods.Count(x => x.Status == LocalModStatus.Enabled);
            var disabled = LocalMods.Count(x => x.Status == LocalModStatus.Disabled);
            try
            {
                log.LogInformation($"collection changed event, length: {LocalMods.Count} / enabled {enabled} / disabled {disabled}");
            }
            catch (Exception)
            {
            }

            var i = 1;
            foreach (var localMod in LocalMods)
            {
                localMod.Order = localMod.Status switch
                {
                    LocalModStatus.Enabled => i++,
                    LocalModStatus.Disabled => null,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
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
