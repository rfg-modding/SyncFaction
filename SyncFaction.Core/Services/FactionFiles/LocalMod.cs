using System.Text.Json.Serialization;

namespace SyncFaction.Core.Services.FactionFiles;

public class LocalMod : IMod
{
    public string Name { get; init; }
    public long Size { get; init; }
    public string? ImageUrl { get; set; }
    public string DownloadUrl { get; init; }
    public string IdString => $"{Name}";
    public string BrowserUrl => string.Empty;
    public DateTime CreatedAt => DateTime.MinValue;
    public string? ImagePath => null;
    public string Markdown => @$"# **{Name}**

Local mod folder in `data/.syncfaction`";
    public override string ToString() => $"{Name}";
}
