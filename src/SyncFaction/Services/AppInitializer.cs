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
using SyncFaction.ViewModels;

namespace SyncFaction.Services;

/// <summary>
/// UI-bound helper to load state and populate data.
/// </summary>
public class AppInitializer
{
    private readonly ILogger<AppInitializer> log;
    private readonly IFileSystem fileSystem;
    private readonly IStateProvider stateProvider;
    private readonly ParallelHelper parallelHelper;

    public AppInitializer(IFileSystem fileSystem, IStateProvider stateProvider, ParallelHelper parallelHelper, ILogger<AppInitializer> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.stateProvider = stateProvider;
        this.parallelHelper = parallelHelper;
    }

    public async Task<bool> Init(ViewModel viewModel, CancellationToken token)
    {
        if (await DetectGame(viewModel, token) == false)
        {
            return false;
        }

        var appStorage = viewModel.Model.GetAppStorage(fileSystem, parallelHelper, log);
        log.LogInformation("Reading current state...");
        var stateFromFile = appStorage.LoadStateFile();
        viewModel.Model.FromState(stateFromFile);
        var firstLaunch = appStorage.Init();
        OnFirstLaunch(firstLaunch);
        var threadCount = viewModel.Model.CalculateThreadCount();
        viewModel.Model.IsGog = await ValidateSteamOrGog(viewModel.Model.IsGog, appStorage, threadCount, token);
        InitStateProvider(viewModel.Model);
        return true;
    }

    private async Task<bool> DetectGame(ViewModel viewModel, CancellationToken token)
    {
        log.LogDebug("Looking for game install path...");
        viewModel.Model.GameDirectory = await AppStorage.DetectGameLocation(log, token);
        if (!string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            return true;
        }

        // force user to locate game
        log.LogWarning("Unable to autodetect game location! Is it GOG version?");
        log.LogWarning("Please locate game manually");
        var dialogSucceeded = false;
        viewModel.ViewAccessor.WindowView.Dispatcher.Invoke(() =>
        {
            using var dialog = new CommonOpenFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Where is the game?"
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                viewModel.Model.GameDirectory = dialog.FileName;
                dialogSucceeded = true;
            }
        });
        if (dialogSucceeded)
        {
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

    private async Task<bool> ValidateSteamOrGog(bool? isGog, AppStorage appStorage, int threadCount, CancellationToken token)
    {
        if (isGog is not null)
        {
            return isGog.Value;
        }

        // SF did not have this flag before so it might be initialized as null
        // or it's first launch and it's really null
        log.LogWarning("Determining if it's Steam or GOG version");
        if (await appStorage.CheckFileHashes(false, threadCount, log, token))
        {
            log.LogInformation("+ **Steam** version");
            return false;
        }

        if (await appStorage.CheckFileHashes(true, threadCount, log, token))
        {
            log.LogInformation("+ **GOG** version");
            return true;
        }

        // refuse to work with FUBAR game files
        log.LogInformation("+ **Unknown** version");
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
