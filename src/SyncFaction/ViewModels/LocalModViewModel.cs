using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

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
