namespace SyncFaction.Core.Services.FactionFiles;

public enum OnlineModStatus
{
    None,
    Ready,
    InProgress,
    Failed
}

[Flags]
public enum ModFlags
{
    None       = 0,
    HasFiles   = 1 << 0,
    HasXDelta  = 1 << 1,
    HasModInfo = 1 << 2,
}
