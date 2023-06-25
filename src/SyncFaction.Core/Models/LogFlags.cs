namespace SyncFaction.Core.Models;

[Flags]
public enum LogFlags
{
    None = 0,
    NoScroll = 1 << 0,
    Clear = 1 << 1,
    Xaml = 1 << 2,
    Bullet = 1 << 3,
    H1 = 1 << 5,
}
