namespace SyncFaction.Core.Services.FactionFiles;

[Flags]
public enum ModFlags
{
    None = 0,
    HasReplacementFiles = 1 << 0,
    HasXDelta = 1 << 1,
    HasModInfo = 1 << 2,
    AffectsMultiplayerFiles = 1 << 3
}
