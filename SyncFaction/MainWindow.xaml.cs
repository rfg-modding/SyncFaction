using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SyncFaction.Services;

namespace SyncFaction;

public partial class MainWindow : Window, IViewAccessor
{
    private readonly ViewModel viewModel;
    private readonly ILogger<MainWindow> log;



    public MainWindow(ViewModel viewModel, MarkdownRender markdownRender, ILogger<MainWindow> log)
    {
        this.viewModel = viewModel;
        this.viewModel.ViewAccessor = this;
        this.log = log;

        Title = SyncFaction.Extras.Title.Value;

        DataContext = viewModel;
        InitializeComponent();

        markdownRender.Init(Markdown);
        markdownRender.Append("# Welcome!");

        Application.Current.DispatcherUnhandledException += (s, e) =>
        {
            log.LogError(e.Exception, $"Unhandled exception! {e.Exception}");
            e.Handled = true;
        };

        CommandBindings.Add(new CommandBinding(
            NavigationCommands.GoToPage,
            (sender, e) =>
            {
                var proc = new Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = (string)e.Parameter;

                proc.Start();
            }));
    }

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Does not know height on first run, that's why another event is needed
    /// </summary>
    private void DevModeGrid_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ChangeWindowSizeForDevModeGrid();
    }

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Required only for first run because height is calculated after visibility event.
    /// </summary>
    private void DevModeGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        devModeGridSize = DevModeGrid.ActualHeight;
        ChangeWindowSizeForDevModeGrid();
        // event is fired again when window size is changed manually and again on visibility toggle after that, to recalculate size. we don't need this at all
        DevModeGrid.SizeChanged -= DevModeGrid_OnSizeChanged;
    }

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Depends on known size of the grid
    /// </summary>
    private void ChangeWindowSizeForDevModeGrid()
    {
        double sign = DevModeGrid.Visibility == Visibility.Visible ? 1 : -1;
        TheWindow.Height += devModeGridSize * sign;
    }

    private double devModeGridSize;

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        viewModel.CloseCommand.Execute(null);
    }

    private void MainWindow_OnContentRendered(object? sender, EventArgs e)
    {
        viewModel.InitCommand.Execute(null);
    }

    public ListView OnlineModListView => OnlineModList;
    public ListView LocalModListView => LocalModList;
}
