namespace SyncFaction.Packer.Services.Peg;

[Flags]
public enum HeaderFlags : uint
{
    Caps =0x1,
    Height = 0x2,
    Width = 0x4,
    PitchUncompressed = 0x8,
    PixelFormat =0x1000,
    MipmapCount =0x20000,
    LinearSizeCompressed =0x80000,
    Depth =0x800000,
    Required = Caps|Height|Width|PixelFormat
}