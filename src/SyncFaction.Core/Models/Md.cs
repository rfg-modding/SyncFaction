namespace SyncFaction.Core.Models;

[Flags]
public enum Md
{
    None = 0,
    NoScroll = 1 << 0,
    Clear = 1 << 1,
    Xaml = 1 << 2,
    Bullet = 1 << 3,
    H1 = 1 << 4,
    Code = 1 << 5,
    Block = 1 << 6,
    B = 1 << 7,
    I = 1 << 8,
    Quote = 1 << 9,
}
