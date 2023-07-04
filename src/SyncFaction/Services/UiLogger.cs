using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Models;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Models;

namespace SyncFaction.Services;

public class UiLogger : ILogger
{
    private readonly MarkdownRender render;
    private readonly IStateProvider stateProvider;
    private readonly string category;

    public UiLogger(MarkdownRender render, IStateProvider stateProvider, string category)
    {
        this.render = render;
        this.stateProvider = stateProvider;
        this.category = category;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // NOTE: default formatter func ignores exception
        // TODO get rid of this ugly hack somehow
        var appState = stateProvider.Initialized
            ? stateProvider.State
            : new State();
        appState.DevMode = true; // TODO REMOVE ME

        if (appState.DevMode is not true && logLevel is LogLevel.Debug or LogLevel.Trace)
        {
            return;
        }

        if (!category.StartsWith("SyncFaction", StringComparison.OrdinalIgnoreCase) && appState.DevMode is not true)
        {
            return;
        }

        if (noisyLoggers.Any(x=> category.Contains(x)))
        {
            return;
        }

        var logFlags = Md.None;
        if (eventId.Id == Constants.LogEventId)
        {
            logFlags = Enum.Parse<Md>(eventId.Name!);
        }

        var autoScroll = !logFlags.HasFlag(Md.NoScroll);

        if (logFlags.HasFlag(Md.Clear))
        {
            if (appState.DevMode is true)
            {
                render.Append("---", true);
            }
            else
            {
                render.Clear();
            }

            return;
        }

        if (logFlags.HasFlag(Md.Xaml))
        {
            var text = formatter(state, exception);
            render.AppendXaml("\n", text, autoScroll);
            return;
        }

        // user-friendly message
        var value = FormatMarkdown(logFlags, logLevel, formatter(state, exception));
        render.Append($"{value}", autoScroll);
        if (exception is not null)
        {
            // collapsed spoiler
            var stacktrace = FormatMarkdown(Md.Block, logLevel, exception.ToString());
            render.Append(stacktrace, autoScroll);
        }
    }

    private static string FormatMarkdown(Md md, LogLevel logLevel, string value)
    {
        value = value.Trim();

        if (!md.HasFlag(Md.Block) && !md.HasFlag(Md.Code))
        {
            if (logLevel is LogLevel.Warning)
            {
                value = $"%{{color:DarkOrange}}{value}%";
            }
            else if (logLevel is LogLevel.Critical or LogLevel.Error)
            {
                value = $"%{{color:Firebrick}}{value}%";
            }
        }

        if (md.HasFlag(Md.H1))
        {
            value = $"# {value}";
        }

        if (md.HasFlag(Md.Bullet))
        {
            value = $"* {value}";
        }

        if (md.HasFlag(Md.B))
        {
            value = $"**{value}**";
        }

        if (md.HasFlag(Md.I))
        {
            value = $"*{value}*";
        }

        if (md.HasFlag(Md.Code))
        {
            value = $"`{value}`";
        }

        if (md.HasFlag(Md.Block))
        {
            value = $"```\n{value}\n```";
        }



        return value;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) => new NopDisposable();

    private readonly HashSet<string> noisyLoggers = new HashSet<string>()
    {
        "System.Net.Http",
        "Microsoft.Extensions.Http",
    };

    private class NopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
