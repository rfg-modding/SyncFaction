using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using MdXaml;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager;
using SyncFaction.Services;

namespace SyncFaction;

public partial class App : Application
{
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
        services.AddSingleton<IFileSystem, FileSystem>();
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
    }
}
