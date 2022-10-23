using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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


    private void Refresh_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowJsonCommand.Execute(null);

        var token = JToken.FromObject(ViewModel);
        var children = new List<JToken>();
        if (token != null)
        {
            children.Add(token);
        }

        Tree.ItemsSource = null;
        Tree.Items.Clear();
        Tree.ItemsSource = children;
    }
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
    public ViewModel()
    {
        ShowJsonCommand = new RelayCommand(ShowJson);
    }

    [ObservableProperty] public string gameDirectory;
    public State State { get; set; } = new State();
    public List<CategoryViewModel> Categories { get; set; } = new();
    [ObservableProperty] public string json;

    public ICommand ShowJsonCommand { get; }

    private void ShowJson()
    {
        Json = null;
        Json = JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}

public class CategoryViewModel
{
    private Category Category { get; set; }
    private List<Mod> Mods { get; set; } = new ();
}
