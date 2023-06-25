using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;

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

        if (category.Contains("HttpMessageHandler") || category.Contains("DefaultHttpClientFactory"))
        {
            // keeps spamming infinitely
            return;
        }

        var audoScroll = !eventId.Name?.EndsWith("false", StringComparison.OrdinalIgnoreCase) ?? true;

        if (eventId.Name == "clear")
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

        if (eventId.Name?.StartsWith("xaml", StringComparison.OrdinalIgnoreCase) == true)
        {
            var text = formatter(state, exception);
            render.AppendXaml("\n", text, audoScroll);
            return;
        }

        var prefix = logLevel switch
        {
            LogLevel.Trace => "> ",
            LogLevel.Debug => "> ",
            LogLevel.Information => string.Empty,
            LogLevel.Warning => "# ",
            LogLevel.Error => "# ",
            LogLevel.Critical => "# ",
            LogLevel.None => string.Empty
        };

        if (category.StartsWith("SyncFaction", StringComparison.OrdinalIgnoreCase))
        {
            render.Append($"{prefix}{formatter(state, exception)}", audoScroll);
        }
        else
        {
            render.Append($"`{logLevel.ToString().Substring(0, 4)} {category.Split('.').Last()} {formatter(state, exception)}`", audoScroll);
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) => new NopDisposable();

    private class NopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
