using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.Core.Models.FactionFiles;

// TODO: split? this is a mix of transport model, convenient bag of properties to pass around, and a view model of sorts
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
public class Mod : IMod
{
    public string IdString => $"{GetType().Name}_{Id}";
    public string BrowserUrl => string.Format(CultureInfo.InvariantCulture, Constants.BrowserUrlTemplate, Id);
    public DateTime CreatedAt => DateTime.UnixEpoch.AddSeconds(UploadTime);

    [JsonIgnore]
    public string Markdown => @$"# **{Name}** by {Author}

*Added: {CreatedAt:yyyy MMMM dd}  #  Downloads: {DownloadCount}*  #  [See on FactionFiles]({BrowserUrl})

{ImageMd}

{DescriptionMd}
";

    private string ImageMd => ImagePath != null
        ? $"![image]({ImagePath})"
        : string.Empty;

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

    [JsonIgnore]
    public ModFlags Flags { get; set; }

    public bool Hide { get; set; }

    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_thumb_4by3_url")]
    public string ImageThumb4By3Url { get; set; }

    public string DownloadUrl { get; set; }
    public string? ImagePath { get; set; }

    [JsonIgnore]
    public ModInfo? ModInfo { get; set; }

    public override string ToString() => $"    {Name}";
}
