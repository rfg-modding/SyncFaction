using Microsoft.Extensions.Logging;

namespace SyncFaction.Services;

public sealed class UiLogBridgeProvider : ILoggerProvider
{
    private readonly MarkdownRender render;

    public UiLogBridgeProvider(MarkdownRender render)
    {
        this.render = render;
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => new UiLogger(render, categoryName);
}
