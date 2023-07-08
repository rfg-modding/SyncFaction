using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Windows;
using Dark.Net;
using MdXaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Filters;
using NLog.Layouts;
using NLog.Targets;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Services;
using SyncFaction.Services;
using SyncFaction.ViewModels;

namespace SyncFaction;

public partial class App
{
    private readonly ServiceProvider serviceProvider;

    internal App()
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

    private static void ConfigureServices(IServiceCollection services)
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
        services.AddSingleton<XmlMagic>();
        services.AddSingleton<XmlHelper>();
        services.AddSingleton<ModLoader>();
        services.AddSingleton<FileChecker>();
        services.AddSingleton<ParallelHelper>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IVppArchiver, VppArchiver>();
        services.AddSingleton<IModInstaller, ModInstaller>();
        services.AddSingleton<IXdeltaFactory, XdeltaFactory>();
        services.AddLogging(static x =>
        {
            x.ClearProviders();
            x.SetMinimumLevel(LogLevel.Trace);
            x.Services.AddSingleton<ILoggerProvider, UiLogBridgeProvider>();
            AddNlog(x);
        });
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Logger owns disposables")]
    private static void AddNlog(ILoggingBuilder x)
    {
        var layout = new JsonLayout
        {
            IncludeScopeProperties = true,
            IncludeEventProperties = true,
            Attributes =
            {
                new("timestamp", "${date:format=O:universalTime=true}"),
                new("level", "${level:upperCase=true}"),
                new("logger", "${logger}"),
                new("message", "${message}"),
                new("exception", "${exception}"),
                new("callsite", "${callsite}")
            }
        };

        var memory = new MemoryTarget("memory");
        memory.Layout = layout;
        var rule = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, memory);

        var filterRule = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, new NullTarget());
        filterRule.Filters.Add(new ConditionBasedFilter
        {
            Action = FilterResult.IgnoreFinal,
            Condition = "'${logger}' == 'Microsoft.Extensions.Http.DefaultHttpClientFactory'"
        });

        var config = new LoggingConfiguration();
        config.AddRule(filterRule);
        config.AddRule(rule);
        x.AddNLog(config);
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

    internal const Theme AppTheme = Theme.Light; // TODO set to auto for release
}
