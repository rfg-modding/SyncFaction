using System.Text;
using System.Text.Json.Serialization;

namespace SyncFaction.Packer.Models.Peg;

public record LogicalTexture(
    Size Size,
    Size Source,
    Size AnimTiles,
    RfgCpeg.Entry.BitmapFormat Format,
    TextureFlags Flags,
    int MipLevels,
    int Order,
    string Name,
    int DataOffset,
    int NameOffset,
    int Align,
    [property:JsonIgnore]Stream Data
)
{
    private readonly int remainder = (int) Data.Length % Align;
    public int PadSize => remainder > 0 ? Align - remainder : 0;
    public int TotalSize => (int)Data.Length + PadSize;
    public int NameOffset { get; internal set; } = NameOffset;
    public byte[] GetNameCString() => Encoding.ASCII.GetBytes(Name + "\0");
    public string EditName = $"{Order:D4} {Path.GetFileNameWithoutExtension(Name)}.png";

}
