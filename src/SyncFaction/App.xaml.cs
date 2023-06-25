using System.IO.Abstractions;
using System.Windows;
using Dark.Net;
using MdXaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Core.Services.Xml;
using SyncFaction.ModManager;
using SyncFaction.Packer.Services;
using SyncFaction.Services;
using SyncFaction.ViewModels;

namespace SyncFaction;

public partial class App : Application
{
    private readonly ServiceProvider serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        DarkNet.Instance.SetCurrentProcessTheme(AppTheme);
        base.OnStartup(e);
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddHttpClient();
        services.AddTransient<MainWindow>();
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

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        /*

        // open second window with flipped dark/light mode for debugging styles:
        var mainWindow2 = new MainWindow(
            new ViewModel(
                serviceProvider.GetRequiredService<ILogger<ViewModel>>(),
                serviceProvider.GetRequiredService<UiCommands>()
                ),
            new MarkdownRender(new Markdown()),
            serviceProvider.GetRequiredService<ILogger<MainWindow>>(),
            true
        );
        mainWindow2.Show();
        */
    }

    public static readonly Theme AppTheme = Theme.Light;
}
