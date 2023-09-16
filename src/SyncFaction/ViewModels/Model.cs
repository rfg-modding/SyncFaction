using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Models;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager.Models;

namespace SyncFaction.ViewModels;

/// <inheritdoc />
/// <summary>
/// UI-bound live model, contains state of game, mods, app, excluding UI-only stuff
/// </summary>
[INotifyPropertyChanged]
public partial class Model
{
    public int ThreadCount => CalculateThreadCount();

    public ObservableCollection<long> TerraformUpdates { get; } = new();

    public ObservableCollection<long> RemoteTerraformUpdates { get; } = new();

    public ObservableCollection<long> ReconstructorUpdates { get; } = new();

    public ObservableCollection<long> RemoteReconstructorUpdates { get; } = new();

    public ObservableCollection<long> AppliedMods { get; } = new();

    public ObservableCollection<long> LastMods { get; } = new();

    /// <summary>
    /// NOTE: not observable, no UI interaction, only save/load on certain actions
    /// </summary>
    public Settings Settings { get; set; } = new();

    [ObservableProperty]
    private string gameDirectory = string.Empty;

    [ObservableProperty]
    private string playerName = string.Empty;

    [ObservableProperty]
    private bool devMode;

    [ObservableProperty]
    private bool useCdn;

    [ObservableProperty]
    private bool startupUpdates = true;

    [ObservableProperty]
    private bool devHiddenMods;

    [ObservableProperty]
    private bool? isGog;

    [ObservableProperty]
    private bool isVerified;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadCount))]
    private bool multithreading;

    internal void FromState(State? state)
    {
        // NOTE: carefully loading state from text file as fields may be missing (from older versions)
        state ??= new State();
        DevMode = state.DevMode ?? false;
        Multithreading = state.Multithreading ?? true;
        IsGog = state.IsGog; // keep nullable because first-time check can be aborted before we know the version
        UseCdn = state.UseCdn ?? true;
        StartupUpdates = state.StartupUpdates ?? true;
        DevHiddenMods = state.DevHiddenMods ?? false;
        IsVerified = state.IsVerified ?? false;
        Settings = state.Settings ?? new Settings();
        PopulateList(state.TerraformUpdates, TerraformUpdates, true);
        PopulateList(state.RslUpdates, ReconstructorUpdates, true);
        PopulateList(state.AppliedMods, AppliedMods, false);
        PopulateList(state.LastMods, LastMods, false);
    }

    internal State ToState() =>
        new()
        {
            DevMode = DevMode,
            Multithreading = Multithreading,
            IsGog = IsGog,
            IsVerified = IsVerified,
            UseCdn = UseCdn,
            StartupUpdates = StartupUpdates,
            DevHiddenMods = DevHiddenMods,
            TerraformUpdates = TerraformUpdates.ToList(),
            RslUpdates = ReconstructorUpdates.ToList(),
            AppliedMods = AppliedMods.ToList(),
            LastMods = LastMods.ToList(),
            Settings = Settings
        };

    /// <summary>
    /// Do not load system at 100% but do not get lower than 1;<br/>
    /// Also limit to some sane value for network calls
    /// </summary>
    private int CalculateThreadCount()
    {
        if (Multithreading == false)
        {
            return 1;
        }

        var almostAllCpus = Environment.ProcessorCount - 2;
        return Math.Clamp(almostAllCpus, 1, 10);
    }

    internal AppStorage GetAppStorage(IFileSystem fileSystem, ILogger log) => new(GameDirectory, fileSystem, log);

    internal GameStorage GetGameStorage(IFileSystem fileSystem, ILogger log) => new(GameDirectory, fileSystem, Hashes.Get(IsGog.Value), log);

    private void PopulateList<T>(IEnumerable<T>? src, ObservableCollection<T> dst, bool autoOrder)
    {
        dst.Clear();
        src ??= new List<T>();
        src = src.Distinct();
        if (autoOrder)
        {
            src = src.OrderBy(static x => x);
        }

        foreach (var modId in src)
        {
            dst.Add(modId);
        }
    }
}
