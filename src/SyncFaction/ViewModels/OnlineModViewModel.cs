using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Models.FactionFiles;

namespace SyncFaction.ViewModels;

[INotifyPropertyChanged]
public partial class OnlineModViewModel : IModViewModel
{
    public string Name => Mod.Name;

    public Category Category => Mod.Category;

    public IMod Mod { get; set; }

    [ObservableProperty]
    private bool selected;

    [ObservableProperty]
    private OnlineModStatus status;

    public OnlineModViewModel(IMod mod)
    {
        Mod = mod;
        // not bothering to sync property in IMod because it won't be reused after any change, whole object is rebuilt on Refresh()
        Status = mod.Status;
    }
}
