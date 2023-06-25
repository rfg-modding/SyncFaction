using System;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Services;

public class StateProvider : IStateProvider
{
    public State State => getter();
    public bool Initialized { get; private set; }
    private Func<State> getter = DefaultGetter;

    public void Init(Model model)
    {
        getter = model.ToState;
        Initialized = true;
    }

    private static State DefaultGetter() => throw new InvalidOperationException("StateProvider is not initialized!");
}
