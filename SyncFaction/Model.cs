using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services;

namespace SyncFaction;

/// <summary>
/// UI-bound live model, contains state of game, mods, app, excluding UI-only stuff
/// </summary>
[INotifyPropertyChanged]
public partial class Model
{
    // TODO: carefully load state from text file as fields may be missing (from older versions)

    [ObservableProperty] private string gameDirectory = string.Empty;

    [ObservableProperty] private bool devMode = true;

    [ObservableProperty] private bool mockMode;

    [ObservableProperty] private bool isGog;

    [ObservableProperty] private bool isVerified;

    [ObservableProperty] private long communityPatch;

    [ObservableProperty] private long newCommunityPatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadCount))]
    private bool multithreading;

    public int ThreadCount => GetThreadCount();

    public Model(PropertyChangedEventHandler? updateCallback)
    {
        // this enables live update of json view of ViewModel
        PropertyChanged += updateCallback;
    }

    public Model()
    {
    }

    public ObservableCollection<long> CommunityUpdates { get; } = new();

    public ObservableCollection<long> NewCommunityUpdates { get; } = new();

    public void LoadFromState(State? state)
    {
        state ??= new State();
        DevMode = state.DevMode ?? false;
        Multithreading = state.Multithreading ?? true;
        IsGog = state.IsGog ?? false;
        MockMode = state.MockMode ?? false;
        IsVerified = state.IsVerified ?? false;
        CommunityPatch = state.CommunityPatch ?? 0;
        CommunityUpdates.Clear();
        var updates = state.CommunityUpdates ?? new List<long>();
        foreach (var update in updates.Distinct().OrderBy(x => x))
        {
            CommunityUpdates.Add(update);
        }
    }

    public State SaveToState() =>
        new()
        {
            CommunityPatch = CommunityPatch,
            DevMode = DevMode,
            Multithreading = Multithreading,
            IsGog = IsGog,
            IsVerified = IsVerified,
            CommunityUpdates = CommunityUpdates.ToList()
        };

    public string GetHumanReadableCommunityVersion(object collectionLock)
    {
        var sb = new StringBuilder();
        sb.Append("base: ");
        sb.Append(CommunityPatch == 0 ? "not installed" : CommunityPatch);
        sb.Append(", updates: ");
        string updates;
        lock (collectionLock)
        {
            updates = string.Join(", ", CommunityUpdates);
        }
        sb.Append(updates == string.Empty ? "none" : updates);
        return sb.ToString();
    }

    public int GetThreadCount()
    {
        if (Multithreading == false)
        {
            return 1;
        }

        // do not load system at 100% but do not get lower than 1
        // also limit to some sane value for network calls
        var almostAllCpus = Environment.ProcessorCount - 2;
        return Math.Clamp(almostAllCpus, 1, 10);
    }
}
