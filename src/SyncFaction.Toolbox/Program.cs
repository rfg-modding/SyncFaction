using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Filters;
using NLog.Layouts;
using SyncFaction.Packer;
using SyncFaction.Toolbox;
using SyncFaction.Toolbox.Args;

var runner = new CommandLineBuilder(new AppRootCommand())
    .UseHost(_ => new HostBuilder(), (builder) => builder
        .ConfigureServices((_, services) =>
        {
            services.AddTransient<IVppArchiver, VppArchiver>();
            services.AddTransient<Commands>();
            services.AddLogging(x =>
            {
                x.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);

                var config = new LoggingConfiguration();

                var console = new NLog.Targets.ConsoleTarget("console");
                console.Layout = Layout.FromString("${date:format=HH\\:MM\\:ss} ${message}");
                var rule = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, console);

                var filterRule = new LoggingRule("Microsoft*", NLog.LogLevel.Trace, NLog.LogLevel.Off, new NLog.Targets.NullTarget());
                filterRule.Filters.Add(new ConditionBasedFilter(){Action = FilterResult.IgnoreFinal, Condition = "'${logger}' == 'Microsoft.Hosting.Lifetime'"});
                filterRule.Filters.Add(new ConditionBasedFilter(){Action = FilterResult.IgnoreFinal, Condition = "'${logger}' == 'Microsoft.Extensions.Hosting.Internal.Host'"});

                config.AddRule(filterRule);
                config.AddRule(rule);

                x.AddNLog(config);
            });
        })
        .UseCommandHandler<AppRootCommand, AppRootCommand.CommandHandler>()
        .UseCommandHandler<Pack, Pack.CommandHandler>()
    )
    .UseDefaults().Build();

await runner.InvokeAsync(args);
