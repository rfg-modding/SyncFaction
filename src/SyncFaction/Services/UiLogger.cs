using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
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
        // TODO get rid of this ugly hack somehow
        var appState = stateProvider.Initialized
            ? stateProvider.State
            : new State();

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

        if (!Enum.TryParse<LogFlags>(eventId.Name, out var logFlags))
        {
            logFlags = LogFlags.None;
        }

        var autoScroll = !logFlags.HasFlag(LogFlags.NoScroll);

        if (logFlags.HasFlag(LogFlags.Clear))
        {
            if (appState.DevMode is true)
            {
                render.Append("---");
            }
            else
            {
                render.Clear();
            }

            return;
        }

        if (logFlags.HasFlag(LogFlags.Xaml))
        {
            var text = formatter(state, exception);
            render.AppendXaml("\n", text, autoScroll);
            return;
        }

        var prefix = GetPrefix(logFlags);
        // NOTE: formatter ignores exception
        if (category.StartsWith("SyncFaction", StringComparison.OrdinalIgnoreCase))
        {
            // user-friendly message
            render.Append($"{prefix}{formatter(state, exception)}", autoScroll);
        }
        else
        {
            // something completely different
            render.Append($"`{logLevel.ToString().PadRight(5)} {category.Split('.').Last()} {formatter(state, exception)}`", autoScroll);
        }

        if (exception is not null)
        {
            render.Append(exception.ToString(), autoScroll);
        }
    }

    private string GetPrefix(LogFlags logFlags)
    {
        if (logFlags.HasFlag(LogFlags.Bullet))
        {
            return "* ";
        }

        if (logFlags.HasFlag(LogFlags.H1))
        {
            return "# ";
        }

        return string.Empty;
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
