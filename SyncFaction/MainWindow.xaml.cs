using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Extras;
using SyncFaction.Services;

namespace SyncFaction;

public partial class MainWindow : Window
{
    private readonly UiCommands uiCommands;
    private readonly MarkdownRender render;
    private readonly StateProvider stateProvider;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<MainWindow> log;
    private readonly CancellationTokenSource cts;
    private readonly CancellationToken token;
    private bool busy;
    private GameStorage gameStorage;
    private readonly IReadOnlyList<Control> interactiveControls;


    public MainWindow(UiCommands uiCommands, MarkdownRender markdownRender, StateProvider stateProvider, IFileSystem fileSystem, ILogger<MainWindow> log)
    {
        this.uiCommands = uiCommands;
        render = markdownRender;
        this.stateProvider = stateProvider;
        this.fileSystem = fileSystem;
        this.log = log;

        cts = new CancellationTokenSource();
        token = cts.Token;
        Title = SyncFaction.Extras.Title.Value;

        InitializeComponent();
    }

    private ViewModel ViewModel => (ViewModel) DataContext;
}

/// <summary>
/// For json tree conversion
/// </summary>
public sealed class MethodToValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var methodName = parameter as string;
        if (value == null || methodName == null)
            return null;
        var methodInfo = value.GetType().GetMethod(methodName, new Type[0]);
        if (methodInfo == null)
            return null;
        return methodInfo.Invoke(value, new object[0]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException(GetType().Name + " can only be used for one way conversion.");
    }
}

[INotifyPropertyChanged]
public partial class ViewModel
{
    // TODO: carefully load state from text file as fields may be missing (from older versions)

    public ViewModel()
    {
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(JsonView))
            {
                // avoid infinite loop
                return;
            }

            // update json view for convenience
            OnPropertyChanged(nameof(JsonView));
        };


        var mods = new List<IMod>
        {
            new Mod()
            {
                Category = Category.ModsRemaster,
                Name = "tool 1"
            },
            new Mod()
            {
                Category = Category.ModsRemaster,
                Name = "tool 2"
            },
            new Mod()
            {
                Category = Category.MapPacks,
                Name = "mappack 3"
            },
            new Mod()
            {
                Category = Category.MapPacks,
                Name = "mappack 1"
            },
            new Mod()
            {
                Category = Category.MapPacks,
                Name = "mappack 2"
            },
            new Mod()
            {
                Category = Category.Local,
                Name = "local 1"
            },
        };
        OnlineMods = new ObservableCollection<IMod>(mods);
    }

    [ObservableProperty] private string gameDirectory = string.Empty;

    [ObservableProperty] private bool devMode = false;

    [ObservableProperty] private bool mockMode = false; // used only for testing

    [ObservableProperty] private bool? isGog = false;

    [ObservableProperty] private bool? isVerified = false;

    [ObservableProperty] private long communityPatch = 0;

    public ObservableCollection<long> CommunityUpdates { get; } = new();

    //public ReadOnlyObservableGroupedCollection<string, IMod> OnlineMods { get; } = new();
    public ObservableCollection<IMod> OnlineMods { get; }

    [JsonIgnore]
    public string JsonView => JsonConvert.SerializeObject(this, Formatting.Indented);

    [RelayCommand]
    private void ClickMe()
    {
    }
}


public class CategoryViewModel
{
    private Category Category { get; set; }
    private List<Mod> Mods { get; set; } = new ();
}
