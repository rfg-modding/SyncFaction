using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace SyncFaction.Services;

public class GogLocator
{
    private readonly IFileSystem fileSystem;
    private readonly ILogger<GogLocator> log;

    public GogLocator(IFileSystem fileSystem, ILogger<GogLocator> log)
    {
        this.fileSystem = fileSystem;
        this.log = log;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I dont care")]
    internal string DetectGogLocation()
    {
        try
        {
            log.LogTrace("Looking for GOG install path");
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\2029222893", false);
            var location = key?.GetValue(@"path") as string;
            if (string.IsNullOrEmpty(location))
            {
                log.LogTrace("GOG location not found in registry");
            }
            else
            {
                log.LogTrace("Found GOG install path [{path}]", location);
                {
                    return location;
                }
            }
        }
        catch (Exception ex)
        {
            log.LogTrace(ex, "Could not autodetect GOG location");
        }

        return string.Empty;
    }

    /// <summary>
    /// Tries to locate gog savegame in appdata. Does not fail if nothing found, just fileinfo of a nonexistent file
    /// </summary>
    /// <example>C:\Users\USER\AppData\Local\GOG.com\Galaxy\Applications\51153410217180642\Storage\Shared\Files\autocloud\save\keen_savegame_0_0.sav</example>
    public IFileInfo DetectGogSavegameFile()
    {
        var appDataLocal = Environment.GetEnvironmentVariable("localappdata");
        var file = fileSystem.FileInfo.New(fileSystem.Path.Join(appDataLocal, @"GOG.com\Galaxy\Applications\51153410217180642\Storage\Shared\Files\autocloud\save\keen_savegame_0_0.sav"));
        return file;
    }
}
