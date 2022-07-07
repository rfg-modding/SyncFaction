using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SyncFaction.Services;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction
{
    public partial class MainWindow : Window
    {
        private readonly UiTools uiTools;
        private readonly MarkdownRender render;
        private readonly CancellationTokenSource cts;
        private readonly CancellationToken token;
        private bool busy;
        private readonly IReadOnlyList<Control> interactiveControls;

        public MainWindow(UiTools uiTools, MarkdownRender markdownRender)
        {
            this.uiTools = uiTools;
            render = markdownRender;

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
            interactiveControls = new List<Control> {run, apply, connect, restore, remoteList};
        }

        public void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            cts.Cancel();
        }

        private async void connect_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Download map lists", async () =>
            {
                remoteList.UnselectAll();
                render.Clear();
                await Task.Run(async () => { await uiTools.Connect(token); }, token);
                remoteList.Items.Clear();
                foreach (var x in uiTools.items)
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
                var dir = new DirectoryInfo(directory.Text);
                await Task.Run(async () => { await uiTools.Restore(dir, token); }, token);
            });
        }

        private async void remoteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Display info", async () =>
            {
                var selectedMod = remoteList.SelectedItem as IMod;
                await Task.Run(async () => { await uiTools.DisplayMod(selectedMod, token); }, token);
            });
        }

        private async void run_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Apply selected mod and run game", async () =>
            {
                var dir = new DirectoryInfo(directory.Text);
                var mod = remoteList.SelectedItem as IMod;
                await Task.Run(async () =>
                {
                    var applied = await uiTools.ApplySelected(dir, mod, token);
                    if (!applied)
                    {
                        return;
                    }

                    render.Append("> Launching game via Steam...");

                    Process.Start(new ProcessStartInfo()
                    {
                        UseShellExecute = true,
                        FileName = "steam://rungameid/667720"
                    });
                }, token);
            });
        }

        private async void apply_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Apply selected mod", async () =>
            {
                var dir = new DirectoryInfo(directory.Text);
                var mod = remoteList.SelectedItem as IMod;
                await Task.Run(async () => { await uiTools.ApplySelected(dir, mod, token); }, token);
            });
        }

        private async Task ExecuteSafeWithUiLock(string description, Func<Task> action)
        {
            if (busy)
            {
                return;
            }

            busy = true;
            foreach (var control in interactiveControls)
            {
                control.IsEnabled = false;
            }

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                var exceptionText = string.Join("\n", ex.ToString().Split('\n').Select(x => $"`` {x} ``"));
                render.Append(string.Format(Constants.ErrorFormat, description, exceptionText), false);
            }
            finally
            {
                busy = false;
                foreach (var control in interactiveControls)
                {
                    control.IsEnabled = true;
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var fail = false;
            await ExecuteSafeWithUiLock("Download map lists", async () =>
            {
                render.Clear();

                var gamePath = await uiTools.Detect(token);
                directory.Text = gamePath;
                var dir = new DirectoryInfo(gamePath);

                remoteList.UnselectAll();
                render.Clear();
                await Task.Run(async () =>
                {
                    var bakDir = uiTools.EnsureBackup(dir);
                    if (bakDir == null)
                    {
                        fail = true;
                        return;
                    }
                    await uiTools.Connect(token);
                }, token);
                remoteList.Items.Clear();
                foreach (var x in uiTools.items)
                {
                    remoteList.Items.Add(x);
                }
            });
            if (fail)
            {
                // disable UI if backup failed, we can't do anything
                foreach (var control in interactiveControls)
                {
                    control.IsEnabled = false;
                }
            }

        }
    }
}

/*
TODO

ensure stock maps on first launch and ask to restore files with Steam if smth is wrong
 */
