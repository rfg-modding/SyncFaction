using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

[INotifyPropertyChanged]
public partial class ModViewModel
{
    private readonly Action<ModViewModel> onSelectedChanged;

    public ModViewModel(IMod mod, bool downloaded, Action<ModViewModel> onSelectedChanged)
    {
        Mod = mod;
        Downloaded = downloaded;
        this.onSelectedChanged = onSelectedChanged;
    }

    [ObservableProperty]
    private bool selected;

    [ObservableProperty]
    private bool downloaded;

    public string Name => Mod.Name;

    public Category Category => Mod.Category;

    public IMod Mod { get; set; }

    /// <summary>
    /// Ugly solution to invoke command from main ViewModel
    /// </summary>
    partial void OnSelectedChanged(bool value)
    {
        onSelectedChanged(this);
    }
}
