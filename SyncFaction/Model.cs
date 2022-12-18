using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncFaction;

[INotifyPropertyChanged]
public partial class Model
{
    // TODO: carefully load state from text file as fields may be missing (from older versions)

    [ObservableProperty] private string gameDirectory = string.Empty;

    [ObservableProperty] private bool devMode = true;

    [ObservableProperty] private bool mockMode = false; // used only for testing

    [ObservableProperty] private bool? isGog = false;

    [ObservableProperty] private bool? isVerified = false;

    [ObservableProperty] private long communityPatch = 0;

    public ObservableCollection<long> CommunityUpdates { get; } = new();
}