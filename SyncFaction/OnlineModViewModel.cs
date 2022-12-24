using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

[INotifyPropertyChanged]
public partial class OnlineModViewModel
{
    public OnlineModViewModel(IMod mod)
    {
        Mod = mod;
        // not bothering to sync property in IMod because it won't be reused after any change, whole object is rebuilt on Refresh()
        Status = mod.Status;
    }

    [ObservableProperty]
    private bool selected;

    [ObservableProperty]
    private ModStatus status;

    public string Name => Mod.Name;

    public Category Category => Mod.Category;

    public IMod Mod { get; set; }
}
