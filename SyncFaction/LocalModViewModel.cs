using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction;

[INotifyPropertyChanged]
public partial class LocalModViewModel : IModViewModel
{
    public LocalModViewModel(IMod mod)
    {
        Mod = mod;
    }

    [ObservableProperty]
    private bool selected;

    [ObservableProperty]
    private int? order;

    [ObservableProperty]
    private LocalModStatus status;

    public ModFlags Flags => Mod.Flags;

    public string Name => Mod.Name;

    public IMod Mod { get; set; }
}
