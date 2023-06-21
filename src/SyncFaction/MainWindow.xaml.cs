﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dark.Net;
using Dark.Net.Wpf;
using GongSolutions.Wpf.DragDrop.Utilities;
using Microsoft.Extensions.Logging;
using SyncFaction.Services;

namespace SyncFaction;

public partial class MainWindow : Window, IViewAccessor
{
    private readonly ViewModel viewModel;
    private readonly ILogger<MainWindow> log;

    public readonly ElementSkinManager SkinManager;

    public MainWindow(ViewModel viewModel, MarkdownRender markdownRender, ILogger<MainWindow> log, bool forceChangeTheme=false)
    {
        this.viewModel = viewModel;
        this.viewModel.ViewAccessor = this;
        this.log = log;

        DataContext = viewModel;
        InitializeComponent();
        Title = Extras.Title.Value;

        var theme = (DarkNet.Instance.EffectiveCurrentProcessThemeIsDark, forceChangeTheme) switch
        {
            (false, false) => Theme.Light,
            (true, false) => Theme.Dark,
            (false, true) => Theme.Dark,
            (true, true) => Theme.Light,
        };
        this.viewModel.Theme = App.AppTheme;
        if (forceChangeTheme)
        {
            this.viewModel.Theme = DarkNet.Instance.EffectiveCurrentProcessThemeIsDark ? Theme.Light : Theme.Dark;
        }
        DarkNet.Instance.SetWindowThemeWpf(this, theme);
        SkinManager = new ElementSkinManager(this);
        SkinManager.RegisterSkins(new Uri("Skins/Skin.Light.xaml", UriKind.Relative), new Uri("Skins/Skin.Dark.xaml", UriKind.Relative));
        SkinManager.UpdateTheme(this.viewModel.Theme);

        /*var light = new Uri("Skins/Skin.Light.xaml", UriKind.Relative);
        var dark = new Uri("Skins/Skin.Dark.xaml", UriKind.Relative);
        Collection<ResourceDictionary> windowResources = Resources.MergedDictionaries;
        var skinResources = windowResources.First(r => r.Source.Equals(light) || r.Source.Equals(dark));
        skinResources.Source = theme == Theme.Dark ? dark : light;*/

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
    public MainWindow WindowView => this;

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateOptionValueText(sender);
    }

    private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateOptionValueText(sender);
    }

    private void UIElement_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateOptionValueText(sender);
    }

    private static void UpdateOptionValueText(object sender)
    {
        var input = sender as DependencyObject;
        var group = input.GetVisualAncestor<GroupBox>();
        if (group is null)
        {
            // event is fired when textbox is removed from selection and it's removed from visual tree also
            return;
        }

        var textBox = group.GetVisualDescendents<TextBox>().LastOrDefault();
        if (textBox is null)
        {
            // skip when textbox is not visible
            return;
        }
        var binding = textBox.GetBindingExpression(TextBox.TextProperty);
        binding.UpdateTarget();
    }

    /// <summary>
    /// Hack to make text near checkbox toggle it
    /// </summary>
    private void DevModeTextClick(object sender, MouseButtonEventArgs e)
    {
        DevMode.IsChecked = !DevMode.IsChecked;
    }
}
