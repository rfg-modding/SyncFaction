using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Services;

namespace SyncFaction;

public partial class MainWindow : Window
{
    private readonly UiCommands uiCommands;
    private readonly MarkdownRender render;
    private readonly StateProvider stateProvider;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<MainWindow> log;
    private readonly CancellationTokenSource cts;
    private readonly CancellationToken token;
    private bool busy;
    private GameStorage gameStorage;
    private readonly IReadOnlyList<Control> interactiveControls;


    public MainWindow(UiCommands uiCommands, MarkdownRender markdownRender, StateProvider stateProvider, IFileSystem fileSystem, ILogger<MainWindow> log)
    {
        this.uiCommands = uiCommands;
        render = markdownRender;
        this.stateProvider = stateProvider;
        this.fileSystem = fileSystem;
        this.log = log;

        InitializeComponent();
        Closing += OnWindowClosing;
        render.Init(text);
        render.Append("# Welcome. Press [connect] button");

        CommandBindings.Add(new CommandBinding(
            NavigationCommands.GoToPage,
            (sender, e) =>
            {


                var proc = new Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = (string)e.Parameter;

                proc.Start();
            }));

        cts = new CancellationTokenSource();
        token = cts.Token;
        interactiveControls = new List<Control> {run, apply, connect, restore, remoteList, update, restore_vanilla};
    }

    public void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        cts.Cancel();
    }

    private async void connect_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Download mod lists", async () =>
        {
            remoteList.UnselectAll();
            render.Clear();
            await Task.Run(async () => { await uiCommands.Connect(token); }, token);
            remoteList.Items.Clear();
            foreach (var x in uiCommands.items)
            {
                remoteList.Items.Add(x);
            }
        });
    }

    private async void restore_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Restore from backup", async () =>
        {
            remoteList.UnselectAll();
            await Task.Run(async () => { await uiCommands.Restore(gameStorage, false, token); }, token);
        });
    }

    private async void restore_vanilla_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Restore from vanilla backup", async () =>
        {
            remoteList.UnselectAll();
            await Task.Run(async () => { await uiCommands.Restore(gameStorage, true, token); }, token);
        });
    }

    private async void remoteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Display info", async () =>
        {
            var selectedMod = remoteList.SelectedItem as IMod;
            await Task.Run(async () => { await uiCommands.DisplayMod(selectedMod, token); }, token);
        });
    }

    private async void apply_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Apply selected mod", async () =>
        {
            var mod = remoteList.SelectedItem as IMod;
            await Task.Run(async () => { await uiCommands.ApplySelected(gameStorage, mod, token); }, token);
        });
    }

    private async void update_Click(object sender, RoutedEventArgs e)
    {
        var success = false;
        await ExecuteSafeWithUiLock("Update to latest Community Patch", async () =>
        {
            await Task.Run(async () => { success = await uiCommands.UpdateCommunityPatch(gameStorage, token); }, token);

            if (success)
            {
                // same as "connect" action: get mod list and news page
                remoteList.UnselectAll();
                render.Clear();
                await Task.Run(async () => { await uiCommands.Connect(token); }, token);
                remoteList.Items.Clear();
                foreach (var x in uiCommands.items)
                {
                    remoteList.Items.Add(x);
                }
            }

        });

        if (!success)
        {
            // disable UI if update failed, we can't do anything
            ToggleInteractiveControls(false);
        }
        else
        {
            ToggleUpdateButton(false);
        }
    }

    private async void run_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("Apply selected mod and run game", async () =>
        {
            var mod = remoteList.SelectedItem as IMod;
            await Task.Run(async () => { await uiCommands.ApplySelectedAndRun(gameStorage, mod, token); }, token);
        });
    }

    private async void devMode_Checked(object sender, RoutedEventArgs e)
    {
        await ExecuteSafeWithUiLock("ToggleDevMode", async () =>
        {
            uiCommands.ToggleDevMode(gameStorage, devMode.IsChecked ?? false);
        });
        if (stateProvider.State.DevMode)
        {
            // if dev mode enabled, unlock normal UI even if there were errors before or if patch is not installed
            ToggleUpdateButton(false);
            ToggleInteractiveControls(true);
            ToggleRestoreVanillaButton(true);
        }
        else
        {
            ToggleRestoreVanillaButton(false);
            ToggleUpdateButton(false);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var success = false;
        await ExecuteSafeWithUiLock("Initialize", async () =>
        {
            render.Clear();
            // try populate game dir input automatically
            var gamePath = await uiCommands.DetectGame(token);
            if (stateProvider.State.MockMode || string.IsNullOrWhiteSpace(gamePath))
            {
                log.LogWarning("Please locate game manually");
                // force user to locate game
                var dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = Directory.GetCurrentDirectory();
                dialog.IsFolderPicker = true;
                dialog.EnsurePathExists = true;
                dialog.Title = "Where is the game?";
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    gamePath = dialog.FileName;
                }
                else
                {
                    log.LogError("Game path unknown. Restart app to try again");
                    return;
                }
                grid.RowDefinitions[3].Height = GridLength.Auto;
            }

            directory.Text = gamePath;
            remoteList.UnselectAll();
            render.Clear();


            var appStorage = new AppStorage(gamePath, fileSystem);
            var firstLaunch = appStorage.InitAppDirectory();
            log.LogInformation("Reading current state...");
            stateProvider.State = appStorage.LoadState() ?? new State();
            if (firstLaunch)
            {
                stateProvider.State.IsVerified = false;
            }
            else
            {
                // SF did not have this flag before but it is guaranteed that game was verified before
                stateProvider.State.IsVerified = true;
            }
            if (firstLaunch || stateProvider.State.IsGog is null)
            {
                log.LogWarning($"Determining if it's Steam or GOG version");
                if (appStorage.CheckFileHashes(false, log))
                {
                    stateProvider.State.IsGog = false;
                    log.LogInformation($"+ **Steam** version");
                }
                else if (appStorage.CheckFileHashes(true, log))
                {
                    stateProvider.State.IsGog = true;
                    log.LogInformation($"+ **GOG** version");
                }
                else
                {
                    log.LogInformation($"+ **Unknown** version");
                }
            }

            if (stateProvider.State.IsGog is null)
            {
                throw new InvalidOperationException("Game version is not recognized as Steam or GOG. Validate your installation and try again.");
            }

            appStorage.WriteState(stateProvider.State);
            gameStorage = new GameStorage(gamePath, fileSystem, Hashes.Get(stateProvider.State.IsGog!.Value));
            await Task.Run(async () => { success = await uiCommands.PopulateData(gameStorage, token); }, token);

            remoteList.Items.Clear();
            foreach (var x in uiCommands.items)
            {
                remoteList.Items.Add(x);
            }

            devMode.IsChecked = stateProvider.State.DevMode;
        });
        if (!success)
        {
            // disable UI if backup failed, we can't do anything
            ToggleInteractiveControls(false);
            return;
        }
        if (uiCommands.newCommunityVersion)
        {
            // enforce update, disable other controls
            ToggleUpdateButton(true);
        }
    }

    /// <summary>
    /// Lock UI, filter duplicate button clicks, display exceptions
    /// </summary>
    private async Task ExecuteSafeWithUiLock(string description, Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        ToggleInteractiveControls(false);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var exceptionText = string.Join("\n", ex.ToString().Split('\n').Select(x => $"`` {x} ``"));
            render.Append("---");
            render.Append(string.Format(Constants.ErrorFormat, description, exceptionText), false);
        }
        finally
        {
            busy = false;
            ToggleInteractiveControls(true);
        }
    }

    /// <summary>
    /// Swap update button in place of Apply button, also hide others
    /// </summary>
    private void ToggleUpdateButton(bool enable)
    {
        if (stateProvider.State.DevMode)
        {
            // dont hide anything
            apply.Visibility = Visibility.Visible;
            run.Visibility = Visibility.Visible;
            update.Visibility = Visibility.Visible;
            restore.Visibility = Visibility.Visible;
            connect.Visibility = Visibility.Visible;
            return;
        }
        if (enable)
        {
            apply.Visibility = Visibility.Hidden;
            run.Visibility = Visibility.Collapsed;
            update.Visibility = Visibility.Visible;
            restore.Visibility = Visibility.Hidden;
            connect.Visibility = Visibility.Hidden;
        }
        else
        {
            apply.Visibility = Visibility.Visible;
            run.Visibility = Visibility.Visible;
            update.Visibility = Visibility.Collapsed;
            restore.Visibility = Visibility.Visible;
            connect.Visibility = Visibility.Visible;
        }
    }

    private void ToggleInteractiveControls(bool enable)
    {
        foreach (var control in interactiveControls)
        {
            control.IsEnabled = enable;
        }
    }

    private void ToggleRestoreVanillaButton(bool enable)
    {
        restore_vanilla.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
    }

}
