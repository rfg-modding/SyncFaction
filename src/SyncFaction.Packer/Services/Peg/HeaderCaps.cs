namespace SyncFaction.Packer.Services.Peg;

[Flags]
public enum HeaderCaps : uint
{
    Complex =0x8,
    Mipmap =0x400000,
    Texture =0x1000,
}