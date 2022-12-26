using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty] private bool devMode;

    [ObservableProperty] private bool mockMode;

    [ObservableProperty] private bool? isGog;

    [ObservableProperty] private bool isVerified;

    [ObservableProperty] private long communityPatch;

    [ObservableProperty] private long newCommunityPatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadCount))]
    private bool multithreading;

    public int ThreadCount => GetThreadCount();

    public ObservableCollection<long> CommunityUpdates { get; } = new();

    public ObservableCollection<long> AppliedMods { get; } = new();

    public ObservableCollection<long> NewCommunityUpdates { get; } = new();

    public void FromState(State? state)
    {
        state ??= new State();
        DevMode = state.DevMode ?? false;
        Multithreading = state.Multithreading ?? true;
        IsGog = state.IsGog;  // keep nullable because first-time check can be aborted before we know the version
        MockMode = state.MockMode ?? false;
        IsVerified = state.IsVerified ?? false;
        CommunityPatch = state.CommunityPatch ?? 0;
        CommunityUpdates.Clear();
        var updates = state.CommunityUpdates ?? new List<long>();
        foreach (var update in updates.Distinct().OrderBy(x => x))
        {
            CommunityUpdates.Add(update);
        }
        AppliedMods.Clear();
        var applied = state.AppliedMods ?? new List<long>();
        foreach (var modId in applied)
        {
            AppliedMods.Add(modId);
        }
    }

    public State ToState() =>
        new()
        {
            CommunityPatch = CommunityPatch,
            DevMode = DevMode,
            Multithreading = Multithreading,
            IsGog = IsGog,
            IsVerified = IsVerified,
            CommunityUpdates = CommunityUpdates.ToList(),
            AppliedMods = AppliedMods.ToList(),

        };

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
