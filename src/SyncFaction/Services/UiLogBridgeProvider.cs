using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Services;

public class UiLogBridgeProvider : ILoggerProvider
{
    private readonly IStateProvider stateProvider;
    private readonly MarkdownRender render;

    public UiLogBridgeProvider(IStateProvider stateProvider, MarkdownRender render)
    {
        this.stateProvider = stateProvider;
        this.render = render;
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => new UiLogger(render, stateProvider, categoryName);
}
