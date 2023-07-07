using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using SyncFaction.Core;
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
    private readonly FileManager fileManager;
    private readonly FileChecker fileChecker;

    public AppInitializer(IFileSystem fileSystem, IStateProvider stateProvider, FileManager fileManager, FileChecker fileChecker, ILogger<AppInitializer> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.stateProvider = stateProvider;
        this.fileManager = fileManager;
        this.fileChecker = fileChecker;
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
        OnFirstLaunch(firstLaunch);
        var threadCount = viewModel.Model.CalculateThreadCount();
        viewModel.Model.IsGog = await ValidateSteamOrGog(viewModel.Model.IsGog, appStorage, threadCount, token);
        InitStateProvider(viewModel.Model);
        return true;
    }

    private async Task<bool> DetectGame(ViewModel viewModel, CancellationToken token)
    {
        log.LogDebug("Looking for game install path");
        viewModel.Model.GameDirectory = await DetectGameLocation(token);
        if (!string.IsNullOrWhiteSpace(viewModel.Model.GameDirectory))
        {
            return true;
        }

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
        log.LogDebug("Determining if it's Steam or GOG version");
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

    private void OnFirstLaunch(bool firstLaunch)
    {
        if (firstLaunch)
        {
            log.LogDebug("This is first launch");
            // TODO nothing special to do here for now?
        }
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "I know registry is windows-only")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I dont care")]
    private async Task<string> DetectGameLocation(CancellationToken token)
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
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

        try
        {
            log.LogDebug("Looking for GOG install path");
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\2029222893", false);
            var location = key?.GetValue(@"path") as string;
            if (string.IsNullOrEmpty(location))
            {
                log.LogDebug("GOG location not found in registry");
            }
            else
            {
                log.LogDebug("Found GOG install path [{path}]", location);
                return location;
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Could not autodetect GOG location");
        }

        try
        {
            log.LogDebug("Looking for Steam install path");
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam", false);
            var steamLocation = key?.GetValue(@"InstallPath") as string;
            if (string.IsNullOrEmpty(steamLocation))
            {
                log.LogDebug("Steam location not found in registry");
            }
            else
            {
                var config = await File.ReadAllTextAsync($@"{steamLocation}\steamapps\libraryfolders.vdf", token);
                var regex = new Regex(@"""path""\s+""(.+?)""");
                var locations = regex.Matches(config).Select(static x => x.Groups).Select(static x => x[1].Value).Select(static x => x.Replace(@"\\", @"\").TrimEnd('\\'));
                const string gamePath = @"steamapps\common\Red Faction Guerrilla Re-MARS-tered";
                foreach (var location in locations)
                {
                    log.LogDebug("Trying steam library at [{location}]", location);
                    var gameDir = Path.Combine(location, gamePath);
                    if (Directory.Exists(gameDir))
                    {
                        log.LogDebug("Found Steam install path [{path}]", gameDir);
                        return gameDir;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Could not autodetect Steam location");
        }

        log.LogWarning("Game is not found nearby, in Gog via registry, or in any of Steam libraries!");
        return string.Empty;
    }
}
