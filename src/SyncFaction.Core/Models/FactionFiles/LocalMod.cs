using System.Text.Json.Serialization;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.Core.Models.FactionFiles;

public class LocalMod : IMod
{
    public string IdString => $"{Name}";
    public string BrowserUrl => string.Empty;
    public DateTime CreatedAt => DateTime.MinValue;
    public string? ImagePath => null;

    public string Markdown => @$"# **{Name}**
Local mod folder in `data/.syncfaction`
";

    public long Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public long Size { get; set; }
    public string DescriptionMd { get; set; }
    public string? ImageUrl { get; set; }
    public string DownloadUrl { get; set; }

    public Category Category { get; set; } = Category.Local;
    public OnlineModStatus Status { get; set; }

    [JsonIgnore]
    public ModFlags Flags { get; set; }

    public bool Hide { get; set; }
    public long UploadTime { get; set; }
    public long DownloadCount { get; set; }

    [JsonIgnore]
    public ModInfo? ModInfo { get; set; }

    public override string ToString() => $"{Name}";
}
