using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;

namespace SyncFaction;

/// <summary>
/// UI-bound app state
/// </summary>
[INotifyPropertyChanged]
public partial class ViewModel
{
    private readonly FfClient ffClient;
    private readonly IFileSystem fileSystem;
    private readonly FileManager fileManager;
    private readonly UiCommands uiCommands;
    private readonly ILogger<ViewModel> log;
    private readonly AppInitializer appInitializer;
    private readonly object collectionLock = new();

    public ViewModel(FfClient ffClient, ILogger<ViewModel> log, IFileSystem fileSystem, AppInitializer appInitializer, FileManager fileManager, UiCommands uiCommands) : this()
    {
        this.ffClient = ffClient;
        this.log = log;
        this.appInitializer = appInitializer;
        this.fileSystem = fileSystem;
        this.fileManager = fileManager;
        this.uiCommands = uiCommands;

        SetDesignTimeDefaults(false);
    }

    /// <summary>
    /// Default constructor for design time. Initializes data, sets up properties, etc
    /// </summary>
    public ViewModel()
    {
        interactiveCommands = new List<IRelayCommand>()
        {
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

    public GroupedDropHandler DropHandler { get; } = new();

    public static readonly SolidColorBrush Highlight = new((Color) ColorConverter.ConvertFromString("#F59408"));

    [ObservableProperty] private Model model;

    [ObservableProperty] private string currentOperation = string.Empty;

    // TODO save to state to remember last opened tab
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DisplayModSettings))]
    private Tab selectedTab = Tab.Apply;

    // TODO show this only when mod has modinfo.xml with inputs?
    public bool DisplayModSettings => SelectedTab == Tab.Apply;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NotInteractive))]
    private bool interactive = true;

    [ObservableProperty] private bool failure = true;

    [ObservableProperty] private bool gridLines;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(UpdateNotRequired))]
    private bool updateRequired = false;

    [ObservableProperty] private int onlineSelectedCount;

    [ObservableProperty] private int localSelectedCount;

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
