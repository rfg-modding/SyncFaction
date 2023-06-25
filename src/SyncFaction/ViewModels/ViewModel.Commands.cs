using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dark.Net;
using Microsoft.Extensions.Logging;
using SyncFaction.Models;

namespace SyncFaction.ViewModels;

public partial class ViewModel
{
    [RelayCommand]
    private void SwitchDarkMode(object x)
    {
        log.LogInformation("switching mode");
        Theme = Theme switch
        {
            Theme.Auto => DarkNet.Instance.EffectiveCurrentProcessThemeIsDark
                ? Theme.Light
                : Theme.Dark,
            Theme.Light => Theme.Dark,
            Theme.Dark => Theme.Light,
            _ => throw new ArgumentOutOfRangeException()
        };
        DarkNet.Instance.SetWindowThemeWpf(ViewAccessor.WindowView, Theme);
        ViewAccessor.WindowView.SkinManager.UpdateTheme(Theme);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Init(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Initializing", uiCommands.Init, token);

    [RelayCommand(CanExecute = nameof(NotInteractive))]
    private void Cancel(object x)
    {
        foreach (var command in cancelCommands)
        {
            command.Execute(x);
        }
    }

    [RelayCommand]
    private void Close(object x)
    {
        Cancel(x);
        uiCommands.WriteState(this);
    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Run(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Fetching FactionFiles data", uiCommands.Run, token);

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Update(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Updating", uiCommands.Update, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Download(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, $"Downloading {OnlineSelectedCount} mods", uiCommands.Download, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Apply(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, $"Applying {LocalSelectedCount} mods", uiCommands.Apply, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Refresh(object x, CancellationToken token)
    {
        switch (SelectedTab)
        {
            case Tab.Download:
                await uiCommands.ExecuteSafe(this, "Fetching FactionFiles data", uiCommands.RefreshOnline, token);
                break;
            case Tab.Apply:
                await uiCommands.ExecuteSafe(this, "Looking for mods", uiCommands.RefreshLocal, token);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [RelayCommand]
    private async Task Display(object x, CancellationToken token)
    {
        var mvm = (IModViewModel) x;
        if (mvm.Selected)
        {
            SelectedMod = mvm;
            await uiCommands.ExecuteSafe(this, "Displaying mod", uiCommands.Display, token);
        }
        else
        {
            SelectedMod = null;
        }
    }

    [RelayCommand]
    private async Task OpenDir(object x, CancellationToken token)
    {
        var arg = x as string ?? string.Empty;
        var destination = Path.Combine(Model.GameDirectory, arg);
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = destination
        });
    }

    [RelayCommand]
    private async Task Test(object x, CancellationToken token) => LocalModCalculateOrder();

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Restore(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Restoring to latest update", uiCommands.Restore, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task RestoreVanilla(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Restoring to vanilla files", uiCommands.RestoreVanilla, token);

    [RelayCommand]
    private void ModResetInputs(object x)
    {
        uiCommands.ModResetInputs(ModInfo, this);
        // NOTE: didnt find a way to update modinfo panel, lets just close it
        SelectedMod.Selected = false;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateReport(object x, CancellationToken token) => await uiCommands.ExecuteSafe(this, "Generating report", uiCommands.GenerateReport, token);
}
