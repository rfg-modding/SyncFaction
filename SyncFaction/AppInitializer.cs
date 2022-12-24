using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Services;

namespace SyncFaction;

/// <summary>
/// UI-bound helper to load state and populate data.
/// </summary>
public class AppInitializer
{
    private readonly ILogger<AppInitializer> log;
    private readonly IFileSystem fileSystem;
    private readonly IStateProvider stateProvider;

    public AppInitializer(IFileSystem fileSystem, IStateProvider stateProvider, ILogger<AppInitializer> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.stateProvider = stateProvider;
    }

    public async Task<bool> Init(Model model, CancellationToken token)
    {
        if (await DetectGame(model, token) == false)
        {
            return false;
        }

        var appStorage = model.GetAppStorage(fileSystem);
        log.LogInformation("Reading current state...");
        var stateFromFile = appStorage.LoadStateFile();
        model.FromState(stateFromFile);
        var firstLaunch = appStorage.InitAppDirectory();
        OnFirstLaunch(firstLaunch);
        var threadCount = model.GetThreadCount();
        model.IsGog = await ValidateSteamOrGog(stateFromFile, appStorage, threadCount, token);
        InitStateProvider(model);
        return true;
    }

    private async Task<bool> DetectGame(Model model, CancellationToken token)
    {
        log.LogDebug("Looking for game install path...");
        model.GameDirectory = await AppStorage.DetectGameLocation(log, token);
        if (!string.IsNullOrWhiteSpace(model.GameDirectory) && !model.MockMode)
        {
            return true;
        }

        // force user to locate game
        log.LogWarning("Unable to autodetect game location! Is it GOG version?");
        log.LogWarning("Please locate game manually");
        using var dialog = new CommonOpenFileDialog
        {
            InitialDirectory = Directory.GetCurrentDirectory(),
            IsFolderPicker = true,
            EnsurePathExists = true,
            Title = "Where is the game?"
        };
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            model.GameDirectory = dialog.FileName;
            return true;
        }

        log.LogError("Game path unknown. Restart app to try again");
        return false;


    }

    private void InitStateProvider(Model model)
    {
        var provider = (StateProvider) stateProvider;
        provider.Init(model);
    }

    private async Task<bool> ValidateSteamOrGog(State? stateFromFile, AppStorage appStorage, int threadCount, CancellationToken token)
    {
        if (stateFromFile?.IsGog is not null)
        {
            return stateFromFile.IsGog.Value;
        }

        // SF did not have this flag before so it might be initialized as null
        // or it's first launch and it's really null
        log.LogWarning($"Determining if it's Steam or GOG version");
        if (appStorage.CheckFileHashes(false, threadCount, log, token))
        {
            log.LogInformation($"+ **Steam** version");
            return false;
        }

        if (appStorage.CheckFileHashes(true, threadCount, log, token))
        {
            log.LogInformation($"+ **GOG** version");
            return true;
        }

        // refuse to work with FUBAR game files
        log.LogInformation($"+ **Unknown** version");
        throw new InvalidOperationException("Game version is not recognized as Steam or GOG. Validate your installation and try again.");
    }

    private void OnFirstLaunch(bool firstLaunch)
    {
        if (firstLaunch)
        {
            log.LogDebug("This is first launch");
            // TODO nothing special to do here for now?
        }
    }
}
