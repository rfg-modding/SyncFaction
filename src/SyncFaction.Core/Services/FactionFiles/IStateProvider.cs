namespace SyncFaction.Core.Services.FactionFiles;

public interface IStateProvider
{
    public State State { get; }
    public bool Initialized { get; }
}
