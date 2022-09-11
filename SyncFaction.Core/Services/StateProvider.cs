using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services;

public class StateProvider
{
    public State State { get; set; } = new ();
}
