using System;
using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using Dark.Net;
using Dark.Net.Wpf;
using MdXaml;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Core.Services.Xml;
using SyncFaction.ModManager;
using SyncFaction.Packer;
using SyncFaction.Services;

namespace SyncFaction;

public partial class App : Application
{
    public static Theme AppTheme = Theme.Auto;

    private ServiceProvider serviceProvider;

    public App()
    {
        ServiceCollection services = new ServiceCollection();
        ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<MainWindow>();
        //services.AddSingleton<TestWindow>();
        services.AddSingleton<FileManager>();
        services.AddSingleton<MarkdownRender>();
        services.AddSingleton<Markdown>();
        services.AddSingleton<FfClient>();
        services.AddSingleton<IStateProvider, StateProvider>();
        services.AddSingleton<ViewModel>();
        services.AddSingleton<AppInitializer>();
        services.AddSingleton<UiCommands>();
        services.AddSingleton<ModTools>();
        services.AddSingleton<XmlMagic>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IVppArchiver, VppArchiver>();
        services.AddSingleton<IModInstaller, ModInstaller>();
        services.AddSingleton<IXdeltaFactory, XdeltaFactory>();
        services.AddLogging(x =>
        {
            x.ClearProviders();
            x.SetMinimumLevel(LogLevel.Trace);
            x.Services.AddSingleton<ILoggerProvider, UiLogBridgeProvider>();
        });
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        DarkNet.Instance.SetCurrentProcessTheme(App.AppTheme);
        new SkinManager().RegisterSkins(new Uri("Skins/Skin.Light.xaml", UriKind.Relative), new Uri("Skins/Skin.Dark.xaml", UriKind.Relative));

        base.OnStartup(e);
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
