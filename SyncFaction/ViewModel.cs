using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

[INotifyPropertyChanged]
public partial class ViewModel
{
    private readonly FfClient ffClient;
    private readonly ILogger<ViewModel> log;

    public ViewModel(FfClient ffClient, ILogger<ViewModel> log) : this()
    {
        this.ffClient = ffClient;
        this.log = log;
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
            RefreshCancelCommand
        };

        PropertyChanged += (s, args) =>
        {
            NotifyInteractiveCommands(args);
            UpdateJsonView(args);
        };

        BindingOperations.EnableCollectionSynchronization(OnlineMods, onlineModsLock);
    }

    private readonly object onlineModsLock = new object();

    /// <summary>
    /// Trigger lock/unlock for all interactive commands
    /// </summary>
    private void NotifyInteractiveCommands(PropertyChangedEventArgs args)
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
    private void UpdateJsonView(PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(JsonView))
        {
            return;

        }

        OnPropertyChanged(nameof(JsonView));
    }

    [ObservableProperty] private Model model = new();

    [ObservableProperty] private string currentOperation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotInteractive))]
    private bool interactive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNotRequired))]
    private bool updateRequired = false;

    public bool NotInteractive => !interactive;

    public bool UpdateNotRequired => !updateRequired;

    public ObservableCollection<IMod> OnlineMods { get; } = new();

    private readonly IReadOnlyList<IRelayCommand> interactiveCommands;

    private readonly List<ICommand> cancelCommands;

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

    [RelayCommand(CanExecute = nameof(Interactive))]
    private void Foo(object x)
    {
        Interactive = !Interactive;
        FooCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Update(object x)
    {

    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private void Run(object x)
    {

    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private void Download(object x)
    {

    }


    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Refresh(object x, CancellationToken token)
    {
        await ExecuteSafeWithUiLock("Fetching FactionFiles data", async () =>
        {
            log.LogInformation("started Refresh cmd, interactive={interactive}", interactive);
            // TODO: loader
            lock (onlineModsLock)
            {
                OnlineMods.Clear();
            }

            var categories = new List<Category>()
            {
                Category.MapPacks,
                Category.MapsStandalone,
                Category.MapsPatches,
            };
            await Parallel.ForEachAsync(categories, new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 3
            }, async (category, cancellationToken) =>
            {
                var mods = await ffClient.GetMods(category, cancellationToken);
                lock (onlineModsLock)
                {
                    // locking whole batch for less noisy UI updates, inserting category by category
                    foreach (var mod in mods)
                    {
                        mod.Category = category;
                        OnlineMods.Add(mod);
                    }
                }
            });
        });
    }

    [RelayCommand(CanExecute = nameof(NotInteractive))]
    private void Cancel(object x)
    {
        foreach (var command in cancelCommands)
        {
            command.Execute(null);
        }
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions
    /// </summary>
    private async Task ExecuteSafeWithUiLock(string description, Func<Task> action)
    {
        if (!Interactive)
        {
            log.LogWarning("Attempt to run command while not intercative, this should not happen normally");
            return;
        }

        // disables all clickable controls
        Interactive = false;
        CurrentOperation = description;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "TODO better exception logging");
            //var exceptionText = string.Join("\n", ex.ToString().Split('\n').Select(x => $"`` {x} ``"));
            //render.Append("---");
            //render.Append(string.Format(Constants.ErrorFormat, description, exceptionText), false);
        }
        finally
        {
            Interactive = true;
            CurrentOperation = string.Empty;
        }
    }

}
