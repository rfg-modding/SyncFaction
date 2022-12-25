using System.IO.Abstractions;
using System.Text.Json.Serialization;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Services.FactionFiles;

public class Mod : IMod
{
    public long Id { get; set; }

    public string Name { get; set; }

    public string Author { get; set; }

    public string Description { get; set; }

    public string DescriptionMd { get; set; }

    public long Size { get; set; }

    public long UploadTime { get; set; }

    public long DownloadCount { get; set; }

    public bool StaffFeatured { get; set; }

    public Category Category { get; set; }

    public OnlineModStatus Status { get; set; }

    public ModFlags Flags { get; set; }

    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_thumb_4by3_url")]
    public string ImageThumb4By3Url { get; set; }

    public string DownloadUrl { get; set; }

    public string IdString => $"{GetType().Name}_{Id}";
    public string BrowserUrl => string.Format(Constants.BrowserUrlTemplate, Id);
    public DateTime CreatedAt => DateTime.UnixEpoch.AddSeconds(UploadTime);
    public string? ImagePath { get; set; }

    public string Markdown => @$"# **{Name}** by {Author}

*Added: {CreatedAt:yyyy MMMM dd}  #  Downloads: {DownloadCount}*  #  [See on FactionFiles]({BrowserUrl}) 

{ImageMd}

{DescriptionMd}
";

    public override string ToString() => $"    {Name}";

    private string ImageMd => ImagePath != null ? $"![image]({ImagePath})" : string.Empty;
}

public class LocalMod : IMod
{
    public long Id { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
    public string? ImageUrl { get; set; }
    public string DownloadUrl { get; set; }
    public string IdString => $"{Name}";
    public string BrowserUrl => string.Empty;
    public DateTime CreatedAt => DateTime.MinValue;
    public string? ImagePath => null;
    public string Markdown => @$"# **{Name}**
Local mod folder in `data/.syncfaction`";

    public Category Category { get; set; } = Category.Local;
    public OnlineModStatus Status { get; set; }
    public ModFlags Flags { get; set; }
    public override string ToString() => $"{Name}";
}
