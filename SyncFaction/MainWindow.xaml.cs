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
using Path = System.IO.Path;

namespace SyncFaction
{
    public partial class MainWindow : Window
    {
        private readonly UiTools uiTools;
        private readonly MarkdownRender render;
        private readonly CancellationTokenSource cts;
        private readonly CancellationToken token;
        private bool busy = false;
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
                var urlText = url.Text;
                render.Clear();
                await Task.Run(async () => { await uiTools.Connect(urlText, token); }, token);
                remoteList.Items.Clear();
                foreach (var playlist in uiTools.playlists)
                {
                    remoteList.Items.Add(playlist.Name.TrimEnd('/'));
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
            await ExecuteSafeWithUiLock("Download readme", async () =>
            {
                var selectedText = remoteList.SelectedItem as string;
                await Task.Run(async () => { await uiTools.GetReadme(selectedText, token); }, token);
            });
        }

        private async void run_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Apply selected playlist and run game", async () =>
            {
                var dir = new DirectoryInfo(directory.Text);
                var selectedText = remoteList.SelectedItem as string;
                await Task.Run(async () =>
                {
                    var applied = await uiTools.ApplyPlaylist(dir, selectedText, token);
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
            await ExecuteSafeWithUiLock("Apply selected playlist", async () =>
            {
                var dir = new DirectoryInfo(directory.Text);
                var selectedText = remoteList.SelectedItem as string;
                await Task.Run(async () => { await uiTools.ApplyPlaylist(dir, selectedText, token); }, token);
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
                render.Append(string.Format(ErrorFormat, description, exceptionText), false);
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

        private readonly string ErrorFormat = @"# Error!
**Operation failed**: {0}

##What now:
* Use Steam to check integrity of game files
* Check if game location is valid and URL is accessible
* See if new versions of SyncFaction are available: [github.com/Rast1234/SyncFaction](https://github.com/Rast1234/SyncFaction)
* Please report this error to developer. **Copy all the stuff below** to help fixing it!
* Take care of your sledgehammer and remain a good Martian

{1}

";

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ExecuteSafeWithUiLock("Download map lists", async () =>
            {
                render.Clear();

                var gamePath = await uiTools.Detect(token);
                directory.Text = gamePath;

                var urlText = url.Text;
                await Task.Run(async () => { await uiTools.Connect(urlText, token); }, token);
                remoteList.Items.Clear();
                foreach (var playlist in uiTools.playlists)
                {
                    remoteList.Items.Add(playlist.Name.TrimEnd('/'));
                }
            });

        }
    }
}

/*
TODO
 */
