using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services;
using SyncFaction.Services;

namespace SyncFaction;

public class UiLogger : ILogger
{
    private readonly MarkdownRender render;
    private readonly StateProvider stateProvider;
    private readonly string category;

    public UiLogger(MarkdownRender render, StateProvider stateProvider, string category)
    {
        this.render = render;
        this.stateProvider = stateProvider;
        this.category = category;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!stateProvider.State.DevMode && logLevel is LogLevel.Debug or LogLevel.Trace)
        {
            return;
        }

        if (!category.StartsWith("SyncFaction") && !stateProvider.State.DevMode)
        {
            return;
        }

        if (category.Contains("HttpMessageHandler") || category.Contains("DefaultHttpClientFactory"))
        {
            // keeps spamming infinitely
            return;
        }

        var audoScroll = !eventId.Name?.EndsWith("false") ?? true;

        if (eventId.Name == "clear")
        {
            if (stateProvider.State.DevMode)
            {
                render.Append("---");
            }
            else
            {
                render.Clear();
            }
            return;
        }

        if (eventId.Name?.StartsWith("xaml") == true)
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
            LogLevel.None => string.Empty,
        };

        if (category.StartsWith("SyncFaction"))
        {
            render.Append($"{prefix}{formatter(state,exception)}", audoScroll);
        }
        else
        {
            render.Append($"`{logLevel.ToString().Substring(0,4)} {category.Split('.').Last()} {formatter(state,exception)}`", audoScroll);
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return new NopDisposable();
    }

    private class NopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
