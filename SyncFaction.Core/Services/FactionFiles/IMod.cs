using System.Text.Json.Serialization;

namespace SyncFaction.Core.Services.FactionFiles;

public interface IMod
{
    public long Id { get; set; }
    public string Name { get; }
    public string Author { get; set; }
    public long Size { get; }
    public string DescriptionMd { get; set; }
    public string IdString { get; }
    public string BrowserUrl { get; }
    public string ImageUrl { get; }
    public string ImagePath { get; }
    public string DownloadUrl { get; }
    public DateTime CreatedAt { get; }
    public string Markdown { get; }
    public Category Category { get; set; }
    public OnlineModStatus Status { get; set; }
    [JsonIgnore]
    public ModFlags Flags { get; set; }
    public bool Hide { get; set; }
    public long UploadTime { get; set; }
    public long DownloadCount { get; set; }
}
