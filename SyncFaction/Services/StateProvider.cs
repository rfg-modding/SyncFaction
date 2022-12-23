using System;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Services;

public class StateProvider : IStateProvider
{
    private Func<State> getter = DefaultGetter;

    public State State => getter();
    public bool Initialized { get; private set; }

    public void Init(Model model)
    {
        getter = model.SaveToState;
        Initialized = true;
    }

    private static State DefaultGetter()
    {
        throw new InvalidOperationException("StateProvider is not initialized!");
    }
}
