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

namespace SyncFaction.Services;

public class SteamLocator
{
    private readonly ILogger<SteamLocator> log;
    private readonly IFileSystem fileSystem;

    public SteamLocator(IFileSystem fileSystem, ILogger<SteamLocator> log)
    {
        this.log = log;
        this.fileSystem = fileSystem;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I dont care")]
    internal async Task<string> DetectSteamGameLocation(CancellationToken token)
    {
        try
        {
            var steamLocation = GetInstallPath();
            if (string.IsNullOrEmpty(steamLocation))
            {
                return string.Empty;
            }

            var config = await fileSystem.File.ReadAllTextAsync($@"{steamLocation}\steamapps\libraryfolders.vdf", token);
            var regex = new Regex(@"""path""\s+""(.+?)""");
            var locations = regex.Matches(config).Select(static x => x.Groups).Select(static x => x[1].Value).Select(static x => x.Replace(@"\\", @"\").TrimEnd('\\'));
            const string gamePath = @"steamapps\common\Red Faction Guerrilla Re-MARS-tered";
            foreach (var location in locations)
            {
                log.LogTrace("Trying steam library at [{location}]", location);
                var gameDir = fileSystem.Path.Combine(location, gamePath);
                if (fileSystem.Directory.Exists(gameDir))
                {
                    log.LogTrace("Found Steam install path [{path}]", gameDir);
                    {
                        return gameDir;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogTrace(ex, "Could not autodetect Steam location");
        }

        return string.Empty;
    }

    /// <summary>
    /// Tries to locate steam install path, then user profile dir with RFG save. Does not fail if no or multiple profiles found, just returns null and writes messages
    /// </summary>
    /// <example>C:\Program Files (x86)\Steam\userdata\STEAMUSERID\667720\remote\autocloud\save\keen_savegame_0_0.sav</example>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I dont care")]
    internal async Task<IFileInfo?> DetectSteamSavegameFile(CancellationToken token)
    {
        //
        try
        {
            var steamLocation = GetInstallPath();
            if (string.IsNullOrEmpty(steamLocation))
            {
                return null;
            }

            var userdata = fileSystem.DirectoryInfo.New(fileSystem.Path.Combine(steamLocation, "userdata"));
            // find all profiles with RFG savegames. fail if more than 1
            var options = new EnumerationOptions()
            {
                RecurseSubdirectories = true
            };
            var rfgSavegamesByProfile = userdata.EnumerateFiles("keen_savegame_0_0.sav", options)
                .Where(x => x.FullName.Replace('\\', '/').EndsWith(@"/667720/remote/autocloud/save/keen_savegame_0_0.sav", StringComparison.OrdinalIgnoreCase))
                .ToList();

            switch (rfgSavegamesByProfile.Count)
            {
                case 0:
                    log.LogInformation("Savegame for Steam version not found");
                    return null;
                case > 1:
                    log.LogError("Found multiple savegames for Steam version from different users");
                    return null;
                default:
                    return rfgSavegamesByProfile.Single();
            }
        }
        catch (Exception ex)
        {
            log.LogTrace(ex, "Could not autodetect Steam savegame location");
        }

        return null;
    }

    private string? GetInstallPath()
    {
        log.LogTrace("Looking for Steam install path");
        using var key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam", false);
        var steamLocation = key?.GetValue(@"InstallPath") as string;
        if (string.IsNullOrEmpty(steamLocation))
        {
            log.LogTrace("Steam location not found in registry");
        }

        return steamLocation;
    }
}
