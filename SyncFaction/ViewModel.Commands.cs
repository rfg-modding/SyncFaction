using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

public partial class ViewModel
{
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Init(object x, CancellationToken token)
    {

        await uiCommands.ExecuteSafe(this, "Initializing", uiCommands.Init, token);

    }

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
    private async Task Run(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, "Fetching FactionFiles data", uiCommands.Run, token);
    }

    [RelayCommand(CanExecute = nameof(Interactive))]
    private async Task Update(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, "Updating", uiCommands.Update, token);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Download(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, $"Downloading {OnlineSelectedCount} mods", uiCommands.Download, token);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Apply(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, $"Applying {-42} mods", uiCommands.Apply, token);
    }

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
        var isLocal = SelectedTab == Tab.Apply;
        var mvm = (IModViewModel)x;
        if (mvm.Selected)
        {
            log.Clear();
            log.LogInformation(new EventId(0, "log_false"), mvm.Mod.Markdown);
            if (isLocal)
            {
                log.LogInformation(new EventId(0, "log_false"), "\n---\n\n" + mvm.Mod.InfoMd());
            }
        }
    }

    [RelayCommand]
    private async Task OpenDir(object x, CancellationToken token)
    {
        var arg = x as string ?? string.Empty;
        var destination = Path.Combine(Model.GameDirectory, arg);
        Process.Start(new ProcessStartInfo()
        {
            UseShellExecute = true,
            FileName = destination
        });
    }

    [RelayCommand]
    private async Task Test(object x, CancellationToken token)
    {
        LocalModCalculateOrder();
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task Restore(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, $"Restoring to latest update", uiCommands.Restore, token);
    }

    [RelayCommand(CanExecute = nameof(Interactive), IncludeCancelCommand = true)]
    private async Task RestoreVanilla(object x, CancellationToken token)
    {
        await uiCommands.ExecuteSafe(this, $"Restoring to vanilla files", uiCommands.RestoreVanilla, token);
    }
}
