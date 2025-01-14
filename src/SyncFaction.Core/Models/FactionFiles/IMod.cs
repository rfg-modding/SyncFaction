using System.Text.Json.Serialization;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.Core.Models.FactionFiles;

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
    public long Id { get; set; }
    public string Author { get; set; }
    public string DescriptionMd { get; set; }
    public Category Category { get; set; }
    public OnlineModStatus Status { get; set; }

    [JsonIgnore]
    public ModFlags Flags { get; set; }

    public bool Hide { get; set; }
    public long UploadTime { get; set; }
    public long DownloadCount { get; set; }

    [JsonIgnore]
    public ModInfo? ModInfo { get; set; }
}
