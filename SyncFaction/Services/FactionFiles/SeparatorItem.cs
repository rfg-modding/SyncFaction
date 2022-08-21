using System;

namespace SyncFaction.Services.FactionFiles;

public class SeparatorItem : IMod
{
    private readonly string value;

    public SeparatorItem(string value)
    {
        this.value = value;
    }

    public string IdString => string.Empty;
    public string BrowserUrl { get; }
    public string Name { get; }
    public long Size { get; }
    public string ImageUrl { get; }
    public string ImagePath { get; }
    public string DownloadUrl { get; }
    public DateTime CreatedAt { get; }
    public string Markdown { get; }

    public override string ToString() => value;
}
