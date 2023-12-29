namespace SyncFaction.Packer.Services.Peg;

[Flags]
public enum PixelFormatFlags : uint
{
    AlphaPixels =0x1,
    Alpha =0x2,
    FourCC =0x4,
    RGB =0x40,
    YUV =0x200,
    Luminance =0x20000,
}