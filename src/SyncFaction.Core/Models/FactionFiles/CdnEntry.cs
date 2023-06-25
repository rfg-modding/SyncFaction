using FastHashes;
using SyncFaction.Core.Data;

namespace SyncFaction.Core.Services.FactionFiles;

public class CdnEntry
{
    public Guid Guid { get; set; }
    public string StorageZoneName { get; set; }
    public string Path { get; set; }
    public string ObjectName { get; set; }
    public long Length { get; set; }
    public DateTime LastChanged { get; set; }
    public long ServerId { get; set; }
    public long ArrayNumber { get; set; }
    public bool IsDirectory { get; set; }
    public Guid UserId { get; set; }
    public string ContentType { get; set; }
    public DateTime DateCreated { get; set; }
    public long StorageZoneId { get; set; }
    public string Checksum { get; set; }
    public string ReplicatedZones { get; set; }

    public Mod ToMod()
    {
        var id = BitConverter.ToInt64(new MurmurHash64().ComputeHash(Guid.ToByteArray()));
        return new Mod
        {
            Id = id,
            Category = Category.Dev,
            Author = "unknown",
            DescriptionMd = "# Mod under development, available from SyncFaction CDN",
            Name = ObjectName,
            Size = Length,
            DownloadUrl = $"{Constants.CdnUrl}/dev/{ObjectName}",
            UploadTime = (long) TimeSpan.FromTicks(DateCreated.Ticks - DateTime.UnixEpoch.Ticks).TotalSeconds
        };
    }
}
