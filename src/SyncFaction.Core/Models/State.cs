using SyncFaction.ModManager.Models;

namespace SyncFaction.Core.Models;

/// <summary>
/// Contains state of game, mods, app. Not bound to UI and is not "live".
/// Used for storing as JSON and to pass data between non-UI services
/// Nullable properties are intended for backwards compatibility!
/// </summary>
public class State
{
    public List<long>? TerraformUpdates { get; set; }
    public List<long>? RslUpdates { get; set; }
    public List<long>? AppliedMods { get; set; }
    public List<long>? LastMods { get; set; }
    public bool? DevMode { get; set; }
    public bool? IsGog { get; set; }
    public bool? IsVerified { get; set; }
    public bool? Multithreading { get; set; }
    public bool? UseCdn { get; set; }
    public Settings? Settings { get; set; }
}
