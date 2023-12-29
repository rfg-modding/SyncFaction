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
using NLog.Targets;
using NLog.Targets.Wrappers;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Services;
using SyncFaction.Packer.Services.Peg;
using SyncFaction.Toolbox;
using SyncFaction.Toolbox.Args;

var runner = new CommandLineBuilder(new AppRootCommand()).UseHost(_ => new HostBuilder(),
        builder => builder.UseConsoleLifetime()
            .ConfigureServices((_, services) =>
            {
                services.AddTransient<IVppArchiver, VppArchiver>();
                services.AddTransient<IPegArchiver, PegArchiver>();
                services.AddTransient<Archiver>();
                services.AddTransient<ImageConverter>();
                services.AddTransient<XmlHelper>();
                services.AddTransient<PegWalker>();
                services.AddLogging(x =>
                {
                    x.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);

                    var config = new LoggingConfiguration();

                    var layout = Layout.FromString("${date:format=HH\\:mm\\:ss} ${pad:padding=5:inner=${level:uppercase=true}} ${message}${onexception:${newline}${exception}}");
                    var console = new ConsoleTarget("console");
                    console.Layout = layout;
                    var rule1 = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, new AsyncTargetWrapper(console, 10000, AsyncTargetWrapperOverflowAction.Discard));

                    var file = new FileTarget("file");
                    file.FileName = ".syncfaction.toolbox.log";
                    file.Layout = layout;
                    var rule2 = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, new AsyncTargetWrapper(file, 10000, AsyncTargetWrapperOverflowAction.Grow));

                    var filterRule = new LoggingRule("*", NLog.LogLevel.Trace, NLog.LogLevel.Off, new NullTarget());
                    filterRule.Filters.Add(new ConditionBasedFilter
                    {
                        Action = FilterResult.IgnoreFinal,
                        Condition = "'${logger}' == 'Microsoft.Hosting.Lifetime'"
                    });
                    filterRule.Filters.Add(new ConditionBasedFilter
                    {
                        Action = FilterResult.IgnoreFinal,
                        Condition = "'${logger}' == 'Microsoft.Extensions.Hosting.Internal.Host'"
                    });
                    filterRule.Filters.Add(new ConditionBasedFilter
                    {
                        Action = FilterResult.IgnoreFinal,
                        Condition = "'${logger}' == 'SyncFaction.Packer.VppArchiver'"
                    });

                    config.AddRule(filterRule);
                    config.AddRule(rule1);
                    config.AddRule(rule2);

                    x.AddNLog(config);
                });
            })
            .UseCommandHandler<AppRootCommand, AppRootCommand.CommandHandler>())
    .AddMiddleware(async (context, next) =>
    {
        try
        {
            await next.Invoke(context);
        }
        catch (Exception e)
        {
            var log = context.GetHost().Services.GetRequiredService<ILogger<Program>>();
            log.LogError(e, "Failed!");
        }
    })
    .UseDefaults()
    .Build();
//args = new[] { "unpeg", @"c:\vault\rfg\unpack_all" };
await runner.InvokeAsync(args);
