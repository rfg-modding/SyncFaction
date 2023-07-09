using System;
using System.Diagnostics;

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
        var themeBefore = Theme;
        Theme = Theme switch
        {
            Theme.Auto => DarkNet.Instance.EffectiveCurrentProcessThemeIsDark
                ? Theme.Light
                : Theme.Dark,
            Theme.Light => Theme.Dark,
            Theme.Dark => Theme.Light,
            _ => throw new ArgumentOutOfRangeException()
        };
        log.LogInformation("Switching theme `{from}` to `{to}`", themeBefore, Theme);
        DarkNet.Instance.SetWindowThemeWpf(ViewAccessor.WindowView, Theme);
        ViewAccessor.WindowView.SkinManager.UpdateTheme(Theme);
        ViewAccessor.WindowView.UpdateDefaultStyle();
        ViewAccessor.WindowView.Markdown.Foreground = ViewAccessor.WindowView.Apply.Foreground;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Init(object x, CancellationToken token) => await ExecuteSafe(this, "Initializing", uiCommands.Init, token, silent: true);

    [RelayCommand(CanExecute = nameof(NotInteractive))]
    private void Cancel(object x)
    {
        log.LogTrace("Cancel");
        foreach (var command in cancelCommands)
        {
            command.Execute(x);
        }

        log.LogWarning("Canceled all running operations");
    }

    [RelayCommand]
    private void Close(object x)
    {
        log.LogTrace("Close");
        Cancel(x);
        uiCommands.WriteState(this);
    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Run(object x, CancellationToken token) => await ExecuteSafe(this, "Launching game", uiCommands.Run, token, silent: true);

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Update(object x, CancellationToken token) => await ExecuteSafe(this, "Updating", uiCommands.Update, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Download(object x, CancellationToken token) => await ExecuteSafe(this, $"Downloading mods", uiCommands.Download, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Apply(object x, CancellationToken token) => await ExecuteSafe(this, $"Applying mods", uiCommands.Apply, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Refresh(object x, CancellationToken token)
    {
        log.LogTrace("Refresh");
        switch (SelectedTab)
        {
            case Tab.Download:
                await ExecuteSafe(this, "Fetching FactionFiles data", uiCommands.RefreshOnline, token);
                break;
            case Tab.Apply:
                await ExecuteSafe(this, "Looking for mods", uiCommands.RefreshLocal, token);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [RelayCommand]
    private async Task Display(object x, CancellationToken token)
    {
        log.LogTrace("Display");
        if (NotInteractive)
        {
            return;
        }

        var mvm = (IModViewModel) x;
        if (mvm.Selected)
        {
            SelectedMod = mvm;
            await ExecuteSafe(this, "Displaying mod", uiCommands.Display, token, silent: true);
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
        var destination = fileSystem.Path.Combine(Model.GameDirectory, arg);
        log.LogTrace("OpenDir [{dir}]", destination);
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = destination
        });
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task RestorePatch(object x, CancellationToken token) => await ExecuteSafe(this, "Restoring to latest patch", uiCommands.RestorePatch, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task RestoreMods(object x, CancellationToken token) => await ExecuteSafe(this, "Restoring last applied mods", uiCommands.RestoreMods, token);

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task RestoreVanilla(object x, CancellationToken token) => await ExecuteSafe(this, "Restoring to vanilla files", uiCommands.RestoreVanilla, token);

    [RelayCommand]
    private void ModResetInputs(object x)
    {
        log.LogTrace("ModResetInputs");
        if (ModInfo is null)
        {
            throw new ArgumentNullException(nameof(ModInfo), "This should not happen");
        }

        if (SelectedMod is null)
        {
            throw new ArgumentNullException(nameof(SelectedMod), "This should not happen");
        }

        uiCommands.ModResetInputs(ModInfo, this);
        // NOTE: didnt find a good way to update modinfo panel, lets just close it
        SelectedMod.Selected = false;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateReport(object x, CancellationToken token) => await ExecuteSafe(this, "Collecting diagnostic info", uiCommands.GenerateReport, token);

    [RelayCommand]
    private async Task GetLogs(object x, CancellationToken token) => await ExecuteSafe(this, "Collecting logs", uiCommands.GetLogs, token);
}
