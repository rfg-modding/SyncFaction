using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Models;

namespace SyncFaction.ViewModels;

[INotifyPropertyChanged]
public partial class LocalModViewModel : IModViewModel
{
    public ModFlags Flags => Mod.Flags;

    public string Name => Mod.Name;

    public IMod Mod { get; set; }

    [ObservableProperty]
    private bool selected;

    [ObservableProperty]
    private int? order;

    [ObservableProperty]
    private LocalModStatus status;

    public LocalModViewModel(IMod mod) => Mod = mod;
}
