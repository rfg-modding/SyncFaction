using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SyncFaction.Core;
using SyncFaction.Core.Models;
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
    private readonly FileChecker fileChecker;
    private readonly SteamLocator steamLocator;
    private readonly GogLocator gogLocator;

    public AppInitializer(IFileSystem fileSystem, IStateProvider stateProvider, FileChecker fileChecker, SteamLocator steamLocator, GogLocator gogLocator, ILogger<AppInitializer> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.stateProvider = stateProvider;
        this.fileChecker = fileChecker;
        this.steamLocator = steamLocator;
        this.gogLocator = gogLocator;
    }

    internal async Task<bool> Init(ViewModel viewModel, CancellationToken token)
    {
        if (await DetectGame(viewModel, token) == false)
        {
            return false;
        }

        var appStorage = viewModel.Model.GetAppStorage(fileSystem, log);
        var stateFromFile = stateProvider.LoadStateFile(appStorage, log);
        viewModel.Model.FromState(stateFromFile);
        var firstLaunch = appStorage.Init();
        viewModel.Model.IsGog = await ValidateSteamOrGog(viewModel.Model.IsGog, appStorage, viewModel.Model.ThreadCount, token);
        OnFirstLaunch(firstLaunch, viewModel.Model.IsGog.Value, viewModel);
        InitStateProvider(viewModel.Model);
        return true;
    }

    private async Task<bool> DetectGame(ViewModel viewModel, CancellationToken token)
    {
        log.LogTrace("Looking for game install path");
        viewModel.Model.GameDirectory = await DetectGameLocation(token);
        if (!string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            return true;
        }

        log.LogWarning("Please locate game manually");
        log.LogInformation(Md.B.Id(), "To avoid doing this every time, put app .exe inside game dir");
        var dialogSucceeded = false;
        viewModel.ViewAccessor.WindowView.Dispatcher.Invoke(() =>
        {
            using var dialog = new CommonOpenFileDialog
            {
                InitialDirectory = fileSystem.Directory.GetCurrentDirectory(),
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

        log.LogError("Game path unknown!");
        return false;
    }

    private void InitStateProvider(Model model)
    {
        var provider = (StateProvider) stateProvider;
        provider.Init(model);
    }

    private async Task<bool> ValidateSteamOrGog(bool? isGog, IAppStorage appStorage, int threadCount, CancellationToken token)
    {
        if (isGog is not null)
        {
            return isGog.Value;
        }

        // SF did not have this flag before so it might be initialized as null
        // or it's first launch and it's really null
        log.LogTrace("Determining if it's Steam or GOG version");
        if (await fileChecker.CheckFileHashes(appStorage, false, threadCount, token))
        {
            log.LogInformation("Game version: **Steam**");
            return false;
        }

        if (await fileChecker.CheckFileHashes(appStorage, true, threadCount, token))
        {
            log.LogInformation("Game version: **GOG**");
            return true;
        }

        // refuse to work with FUBAR game files
        log.LogError("Game version: **unknown**");
        throw new InvalidOperationException("Game version is not recognized as Steam or GOG");
    }

    private void OnFirstLaunch(bool firstLaunch, bool isGog, ViewModel viewModel)
    {
        if (!firstLaunch)
        {
            return;
        }

        log.LogTrace("This is first launch. isGog [{isGog}]", isGog);
        if (!isGog)
        {
            OneTimeCopySteamSaves(viewModel);
        }
    }

    private void OneTimeCopySteamSaves(ViewModel viewModel)
    {
        log.LogInformation(Md.B.Id(), "One-time prompt to copy saves from Steam version to GOG");
        viewModel.ViewAccessor.WindowView.Dispatcher.Invoke(() =>
        {
            var message = @"Important question!

This message will be displayed only once.

Your Steam version of the game will be converted to GOG to simplify modding and community support.

Do you want to transfer your savegames, progess, profile and settings to a directory where GOG version expects them? Simply put, keep all your in-game data.

Typically [ Yes ] is a good choice. However, you may want to have both GOG and Steam game versions with separate settings and progress.
";
            var result = MessageBox.Show(message, "Savegame transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);
            log.LogTrace("Messagebox result [{result}]", result);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            viewModel.CopySaveToGogCommand.Execute(null);
        });
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "I know registry is windows-only")]
    private async Task<string> DetectGameLocation(CancellationToken token)
    {
        var currentDir = fileSystem.DirectoryInfo.New(fileSystem.Directory.GetCurrentDirectory());
        var entries = currentDir.GetFileSystemInfos();
        if (entries.Any(static x => x.Name.Equals(Constants.StateFile, StringComparison.OrdinalIgnoreCase)))
        {
            // we are in "game/data/.syncfaction/"
            return currentDir.Parent!.Parent!.FullName;
        }

        if (entries.Any(static x => x.Name.Equals(Constants.AppDirName, StringComparison.OrdinalIgnoreCase)))
        {
            // we are in "game/data/"
            return currentDir.Parent!.FullName;
        }

        if (entries.Any(static x => x.Name.Equals("table.vpp_pc", StringComparison.OrdinalIgnoreCase)))
        {
            // we are in "game/data/" and SF was not present before
            return currentDir.Parent!.FullName;
        }

        if (entries.Any(static x => x.Name.Equals("data", StringComparison.OrdinalIgnoreCase)))
        {
            // we are in "game/"
            return currentDir.FullName;
        }

        var gog = gogLocator.DetectGogLocation();
        var steam = await steamLocator.DetectSteamGameLocation(token);

        // TODO REMOVE ME (debugging)
        return steam;

        if (!string.IsNullOrEmpty(gog) && string.IsNullOrEmpty(steam))
        {
            return gog;
        }

        if (!string.IsNullOrEmpty(steam) && string.IsNullOrEmpty(gog))
        {
            return steam;
        }

        if (!string.IsNullOrEmpty(steam) && !string.IsNullOrEmpty(gog))
        {
            log.LogWarning("Found both GOG and Steam versions, can't decide automatically");
            return string.Empty;
        }

        log.LogWarning("Game is not found nearby, in GOG via registry, or in any of Steam libraries!");
        return string.Empty;
    }


}
