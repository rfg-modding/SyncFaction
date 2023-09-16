using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dark.Net;
using GongSolutions.Wpf.DragDrop.Utilities;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SyncFaction.Packer.Models;
using SyncFaction.Services;
using SyncFaction.ViewModels;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using GroupBox = System.Windows.Controls.GroupBox;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;

namespace SyncFaction;

[SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Why not?")]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "I dont care about Player")]
public partial class MainWindow : Window, IViewAccessor
{
    public ListView OnlineModListView => OnlineModList;
    public ListView LocalModListView => LocalModList;
    public MainWindow WindowView => this;

    public readonly ElementSkinManager SkinManager;
    private readonly ViewModel viewModel;
    private readonly ILogger<MainWindow> log;

    private double devModeGridSize;

    public MainWindow(ViewModel viewModel, MarkdownRender markdownRender, ILogger<MainWindow> log, bool flipTheme = false)
    {
        this.viewModel = viewModel;
        this.viewModel.ViewAccessor = this;
        this.log = log;

        DataContext = viewModel;
        InitializeComponent();
        Title = Extras.Title.Value;

        var theme = (DarkNet.Instance.EffectiveCurrentProcessThemeIsDark, flipTheme) switch
        {
            (false, false) => Theme.Light,
            (true, false) => Theme.Dark,
            (false, true) => Theme.Dark,
            (true, true) => Theme.Light
        };
        this.viewModel.Theme = App.AppTheme;
        if (flipTheme)
        {
            this.viewModel.Theme = DarkNet.Instance.EffectiveCurrentProcessThemeIsDark
                ? Theme.Light
                : Theme.Dark;
        }


        DarkNet.Instance.SetWindowThemeWpf(this, theme);
        SkinManager = new ElementSkinManager(this);
        SkinManager.RegisterSkins(new Uri("Skins/Skin.Light.xaml", UriKind.Relative), new Uri("Skins/Skin.Dark.xaml", UriKind.Relative));
        SkinManager.UpdateTheme(this.viewModel.Theme);

        markdownRender.Init(Markdown);
        Markdown.Foreground = Apply.Foreground;
        markdownRender.Append("# Welcome!", true);

        Application.Current.DispatcherUnhandledException += (_, e) =>
        {
            log.LogError(e.Exception, "Unhandled exception!");
            e.Handled = true;
        };

        CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage,
            static (_, e) =>
            {
                var proc = new Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = (string) e.Parameter;
                proc.Start();
            }));

        var assembly = Application.ResourceAssembly;
        var resources = assembly.GetManifestResourceNames().Single();
        using var resourceSet = new ResourceSet(assembly.GetManifestResourceStream(resources));
        wav = (Stream)resourceSet.GetObject("hammer.wav");
    }

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Does not know height on first run, that's why another event is needed
    /// </summary>
    private void DevModeGrid_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => ChangeWindowSizeForDevModeGrid();

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Required only for first run because height is calculated after visibility event.
    /// </summary>
    private void DevModeGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        devModeGridSize = DevModeGrid.ActualHeight;
        ChangeWindowSizeForDevModeGrid();
        // NOTE: event is fired again when window size is changed manually and again on visibility toggle after that, to recalculate size. we don't need this at all
        DevModeGrid.SizeChanged -= DevModeGrid_OnSizeChanged;
    }

    /// <summary>
    /// Expands window if dev mode grid is shown and vice versa. Depends on known size of the grid
    /// </summary>
    private void ChangeWindowSizeForDevModeGrid()
    {
        double sign = DevModeGrid.Visibility == Visibility.Visible
            ? 1
            : -1;
        TheWindow.Height += devModeGridSize * sign;
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e) => viewModel.CloseCommand.Execute(null);

    private void MainWindow_OnContentRendered(object? sender, EventArgs e) => viewModel.InitCommand.Execute(null);

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOptionValueText(sender);

    private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateOptionValueText(sender);

    private void UIElement_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateOptionValueText(sender);

    /// <summary>
    /// Hack to make text near checkbox toggle it
    /// </summary>
    private void DevModeTextClick(object sender, MouseButtonEventArgs e) => DevMode.IsChecked = !DevMode.IsChecked;

    private void ModResetInputs_Click(object sender, RoutedEventArgs e) => viewModel.ModResetInputsCommand.Execute(null);

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(viewModel.DiagView);
        log.LogInformation("Copied report to clipboard");
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

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private void LegoMasonClick(object sender, MouseButtonEventArgs e)
    {
        var waveProvider = new RawSourceWaveStream(new StreamView(wav, 0, wav.Length), new WaveFormat(44100, 16, 1));
        var waveOut = new WaveOut();
        waveOut.PlaybackStopped += OnPlaybackStopped;
        waveOut.Init(waveProvider);
        waveOut.Play();

        void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            waveProvider.Dispose();
            waveOut.Dispose();
        }

        clicks++;
        var divisor = 5;
        if (clicks % divisor == 0)
        {
            var index = (clicks / divisor) - 1;
            if (index < Messages.Count)
            {
                var (header, text, button, image) = Messages[index];
                MessageBox.Show(text, header, button, image);
            }

            if (index == Messages.Count - 1)
            {
                using var proc = new Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kUXc0dzlXZ1hjUQ=="));
                proc.Start();
            }
        }
    }

    private int clicks;

    private readonly IReadOnlyList<MessageboxParams> Messages = new List<MessageboxParams>
    {
        new("Hammertime!", "Enjoying the sound?", MessageBoxButton.YesNo, MessageBoxImage.Question),
        new("Hammertime!", "Brings back ModManager memories, yea?", MessageBoxButton.YesNo, MessageBoxImage.Question),
        new("Hammertime!", "You know, this Lego Mason is not a render. I built him for real!", MessageBoxButton.OK, MessageBoxImage.Asterisk),
        new("Hammertime!", "Greetings to:\nCamo\nmoneyl\nGoober", MessageBoxButton.OK, MessageBoxImage.Asterisk),
        new("Hammertime!", "Clicking this all day won't help you unlock ostrich sledgehammer!", MessageBoxButton.OK, MessageBoxImage.Warning),
        new("Hammertime!", "Stop or i'll call EDF", MessageBoxButton.YesNo, MessageBoxImage.Error),
        new("Hammertime!", "No really", MessageBoxButton.OK, MessageBoxImage.Error),
        new("Hammertime!", "Actually, i have a better idea!", MessageBoxButton.OK, MessageBoxImage.Asterisk),
    };

    private readonly Stream wav;

    private record MessageboxParams(string Header, string Text, MessageBoxButton button, MessageBoxImage image);
}
