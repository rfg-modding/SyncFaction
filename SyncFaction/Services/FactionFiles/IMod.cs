using System;

namespace SyncFaction.Services.FactionFiles;

public interface IMod
{
    public string Name { get; }
    public long Size { get; }
    public string IdString { get; }
    public string BrowserUrl { get; }
    public string ImageUrl { get; }
    public string ImagePath { get; }
    public string DownloadUrl { get; }
    public DateTime CreatedAt { get; }
    public string Markdown { get; }
}
