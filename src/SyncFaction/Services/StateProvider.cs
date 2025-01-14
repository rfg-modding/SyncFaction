using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Models;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ViewModels;

namespace SyncFaction.Services;

public class StateProvider : IStateProvider
{
    // NOTE: logger depended on StateProvider, that's why not injecting ILog here
    public State State => getter();
    public bool Initialized { get; private set; }
    private Func<State> getter = DefaultGetter;

    public void Init(Model model)
    {
        getter = model.ToState;
        Initialized = true;
    }

    private static State DefaultGetter() => throw new InvalidOperationException("StateProvider is not initialized!");

    public State? LoadStateFile(AppStorage appStorage, ILogger log)
    {
        log.LogInformation("Loading settings...");
        var file = appStorage.FileSystem.FileInfo.New(appStorage.FileSystem.Path.Combine(appStorage.App.FullName, Constants.StateFile));
        if (!file.Exists)
        {
            log.LogTrace("State file does not exist");
            return null;
        }

        log.LogTrace("Reading state file (size: {size})", file.Length);
        var content = appStorage.FileSystem.File.ReadAllText(file.FullName).Trim();
        return JsonSerializer.Deserialize<State>(content);
    }

    public void WriteStateFile(AppStorage appStorage, State state, ILogger log)
    {
        var file = appStorage.FileSystem.FileInfo.New(appStorage.FileSystem.Path.Combine(appStorage.App.FullName, Constants.StateFile));
        if (file.Exists)
        {
            file.Delete();
            log.LogTrace("Deleted file [{file}]", file.FullName);
        }

        var data = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        appStorage.FileSystem.File.WriteAllText(file.FullName, data);
        log.LogTrace("Saved state to [{file}]", file.FullName);
    }
}
