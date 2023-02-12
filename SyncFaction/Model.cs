using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services;
using SyncFaction.ModManager;

namespace SyncFaction;

/// <summary>
/// UI-bound live model, contains state of game, mods, app, excluding UI-only stuff
/// </summary>
[INotifyPropertyChanged]
public partial class Model
{
    [ObservableProperty] private string gameDirectory = string.Empty;

    [ObservableProperty] private bool devMode;

    [ObservableProperty] private bool useCdn;

    [ObservableProperty] private bool? isGog;

    [ObservableProperty] private bool isVerified;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadCount))]
    private bool multithreading;

    public int ThreadCount => GetThreadCount();

    public ObservableCollection<long> TerraformUpdates { get; } = new();

    public ObservableCollection<long> NewTerraformUpdates { get; } = new();

    public ObservableCollection<long> RslUpdates { get; } = new();

    public ObservableCollection<long> NewRslUpdates { get; } = new();

    public ObservableCollection<long> AppliedMods { get; } = new();

    /// <summary>
    /// NOTE: not observable, no UI interaction, only save/load on certain actions
    /// </summary>
    public Settings Settings { get; set; } = new Settings();

    public void FromState(State? state)
    {
        // NOTE: carefully loading state from text file as fields may be missing (from older versions)
        state ??= new State();
        DevMode = state.DevMode ?? false;
        Multithreading = state.Multithreading ?? true;
        IsGog = state.IsGog;  // keep nullable because first-time check can be aborted before we know the version
        UseCdn = state.UseCdn ?? true;
        IsVerified = state.IsVerified ?? false;
        Settings = state.Settings ?? new Settings();
        PopulateList(state.TerraformUpdates, TerraformUpdates, true);
        PopulateList(state.RslUpdates, RslUpdates, true);
        PopulateList(state.AppliedMods, AppliedMods, false);
    }

    public State ToState() =>
        new()
        {
            DevMode = DevMode,
            Multithreading = Multithreading,
            IsGog = IsGog,
            IsVerified = IsVerified,
            UseCdn = UseCdn,
            TerraformUpdates = TerraformUpdates.ToList(),
            RslUpdates = RslUpdates.ToList(),
            AppliedMods = AppliedMods.ToList(),
            Settings = Settings,
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

    private void PopulateList<T>(IEnumerable<T>? src, ObservableCollection<T> dst, bool order)
    {
        dst.Clear();
        src ??= new List<T>();
        src = src.Distinct();
        if (order)
        {
            src = src.OrderBy(x => x);
        }
        foreach (var modId in src)
        {
            dst.Add(modId);
        }
    }
}
