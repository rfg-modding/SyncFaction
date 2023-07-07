using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dark.Net;
using Microsoft.Extensions.Logging;
using SyncFaction.Models;
using SyncFaction.ModManager.XmlModels;
using SyncFaction.Services;

namespace SyncFaction.ViewModels;

/// <inheritdoc />
/// <summary>
/// UI-bound app state
/// </summary>
[INotifyPropertyChanged]
public partial class ViewModel
{
    public GroupedDropHandler DropHandler { get; } = new();

    public bool DisplayModSettings => SelectedTab == Tab.Apply && ModInfo is not null;

    private ModInfo? ModInfo => (SelectedMod as LocalModViewModel)?.Mod.ModInfo;

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

    internal IViewAccessor ViewAccessor { get; set; } = null!;

    internal Theme Theme { get; set; }

    internal string? LastException { get; set; }

    private readonly UiCommands uiCommands = null!;

    private readonly ILogger<ViewModel> log = null!;

    private readonly object collectionLock = new();

    /// <summary>
    /// Commands to disable while running something
    /// </summary>
    private readonly IReadOnlyList<IRelayCommand> interactiveCommands;

    /// <summary>
    /// Commands to cancel manually
    /// </summary>
    private readonly List<ICommand> cancelCommands;

    [ObservableProperty]
    private Model model;

    [ObservableProperty]
    private string currentOperation = string.Empty;

    // TODO save to state to remember last opened tab
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModSettings))]
    private Tab selectedTab = Tab.Apply;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotInteractive))]
    private bool interactive = true;

    [ObservableProperty]
    private bool generalFailure;

    [ObservableProperty]
    private bool gridLines;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNotRequired))]
    private bool updateRequired;

    [ObservableProperty]
    private int onlineSelectedCount;

    [ObservableProperty]
    private int localSelectedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModSettings))]
    private IModViewModel? selectedMod;

    /// <summary>
    /// View of diagnostics output
    /// </summary>
    [ObservableProperty]
    private string? diagView;

    public ViewModel(ILogger<ViewModel> log, UiCommands uiCommands) : this()
    {
        this.log = log;
        this.uiCommands = uiCommands;

        SetDesignTimeDefaults(false);
    }

    /// <summary>
    /// Default constructor for design time. Initializes data, sets up properties, etc
    /// </summary>
    public ViewModel()
    {
        interactiveCommands = new List<IRelayCommand>
        {
            RefreshCommand,
            RunCommand,
            DownloadCommand,
            ApplyCommand,
            CancelCommand,
            RestoreModsCommand,
            RestorePatchCommand,
            RestoreVanillaCommand
        };

        cancelCommands = new List<ICommand>
        {
            InitCancelCommand,
            RefreshCancelCommand,
            DownloadCancelCommand,
            ApplyCancelCommand,
            RestoreModsCancelCommand,
            RestorePatchCancelCommand,
            RestoreVanillaCancelCommand,
            GenerateReportCancelCommand
        };

        PropertyChanged += NotifyInteractiveCommands;
        model = new Model();
        LocalMods.CollectionChanged += LocalModsOnCollectionChanged;

        // this allows other threads to work with UI-bound collection
        BindingOperations.EnableCollectionSynchronization(OnlineMods, collectionLock);
        BindingOperations.EnableCollectionSynchronization(LocalMods, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.TerraformUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.RemoteTerraformUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.RslUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.RemoteRslUpdates, collectionLock);
        BindingOperations.EnableCollectionSynchronization(Model.AppliedMods, collectionLock);

        SetDesignTimeDefaults(true);
    }

    internal static readonly SolidColorBrush Highlight = new((Color) ColorConverter.ConvertFromString("#F59408"));
}
