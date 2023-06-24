namespace SyncFaction.Core.Services.Files;

public enum FileKind
{
    /// <summary>
    /// File exists in base game distribution
    /// </summary>
    Stock,

    /// <summary>
    /// File is introduced by patch and should be preserved
    /// </summary>
    FromPatch,

    /// <summary>
    /// File is introduced by mod and should be removed
    /// </summary>
    FromMod,

    /// <summary>
    /// File is created by user or game and should be ignored
    /// </summary>
    Unmanaged
}