using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using MdXaml;
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
        services.AddSingleton<Tools>();
        services.AddSingleton<UiTools>();
        services.AddSingleton<MarkdownRender>();
        services.AddSingleton<Markdown>();
        services.AddSingleton<FfClient>();
        services.AddLogging(x => x.ClearProviders());
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}