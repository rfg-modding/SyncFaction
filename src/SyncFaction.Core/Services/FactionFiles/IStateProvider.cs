using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Services.FactionFiles;

public interface IStateProvider
{
    public State State { get; }
    public bool Initialized { get; }
    State? LoadStateFile(AppStorage appStorage, ILogger log);
    void WriteStateFile(AppStorage appStorage, State state, ILogger log);
}
