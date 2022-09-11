using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services;
using SyncFaction.Services;

namespace SyncFaction;

public class UiLogBridgeProvider : ILoggerProvider
{
    private readonly MarkdownRender render;
    private readonly StateProvider stateProvider;

    public UiLogBridgeProvider(MarkdownRender render, StateProvider stateProvider)
    {
        this.render = render;
        this.stateProvider = stateProvider;
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new UiLogger(render, stateProvider, categoryName);
    }
}