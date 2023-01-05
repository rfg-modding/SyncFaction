using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Core.Services.Files;

public class AppStorage: IAppStorage {

	internal readonly IFileSystem fileSystem;

	public AppStorage(string gameDir, IFileSystem fileSystem)
	{
		this.fileSystem = fileSystem;
		Game = fileSystem.DirectoryInfo.FromDirectoryName(gameDir);
		if (!Game.Exists)
		{
			throw new ArgumentException($"Specified game directory does not exist! [{Game.FullName}]");
		}

		Data = Game.GetDirectories().Single(x => x.Name == "data");
		App = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(Data.FullName, Constants.AppDirName));
		Img = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(App.FullName, Constants.ImgDirName));
	}

	public IDirectoryInfo App { get; }

    public IDirectoryInfo Img { get; }

	public IDirectoryInfo Game { get; }

	public IDirectoryInfo Data { get; }

	public State? LoadStateFile()
	{
		var file = new FileInfo(Path.Combine(App.FullName, Constants.StateFile));
		if (!file.Exists)
		{
			return null;
		}

		var content = File.ReadAllText(file.FullName).Trim();
		return JsonSerializer.Deserialize<State>(content);
	}

	public void WriteStateFile(State state)
	{
		var file = new FileInfo(Path.Combine(App.FullName, Constants.StateFile));
		if (file.Exists)
		{
			file.Delete();
		}

		var data = JsonSerializer.Serialize(state, new JsonSerializerOptions() {WriteIndented = true});
		File.WriteAllText(file.FullName, data);
	}

	public string ComputeHash(IFileInfo file)
	{
		using var sha = SHA256.Create();
		using var fileStream = file.Open(FileMode.Open);
		fileStream.Position = 0;
		var hashValue = sha.ComputeHash(fileStream);
		var hash = BitConverter.ToString(hashValue).Replace("-", "");
		return hash;
	}

	public bool Init()
    {
        bool createdAppDir = false;
		if (!App.Exists)
		{
			App.Create();
            createdAppDir = true;
        }

        if (!Img.Exists)
        {
            Img.Create();
        }

		return createdAppDir;
	}


    public bool CheckFileHashes(bool isGog, int threadCount, ILogger log, CancellationToken token)
	{
		var files = isGog ? Hashes.Gog : Hashes.Steam;

		var versionName = isGog ? nameof(Hashes.Gog) : nameof(Hashes.Steam);
        // not checking version-specific VPP files just to detect version, it's very slow
        var result = Parallel.ForEach(files.Where(x => !x.Key.EndsWith(".vpp_pc")).OrderBy(x => x.Key), new ParallelOptions()
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = threadCount
        }, (kv, loopState) =>
        {
            //var file = new GameFile(this, kv.Key, fileSystem);
            var path = Path.Combine(Game.FullName, kv.Key);
            var fileInfo = fileSystem.FileInfo.FromFileName(path);
            var expected = kv.Value;
            var hash = fileInfo.Exists ? ComputeHash(fileInfo) : null;
            var isVanilla = (hash ?? string.Empty).Equals(expected, StringComparison.OrdinalIgnoreCase);
            if (!isVanilla)
            {
                log.LogDebug("Checking for [{}] version failed: file mismatch `{}`", versionName, fileInfo.Name);
                loopState.Stop();
            }
        });

        return result.IsCompleted;
    }

	public static async Task<string> DetectGameLocation(ILogger log, CancellationToken token)
	{
		var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
		var entries = currentDir.GetFileSystemInfos();
		if (entries.Any(x => x.Name.Equals(Constants.StateFile, StringComparison.OrdinalIgnoreCase)))
		{
			// we are in "game/data/.syncfaction/"
			return currentDir.Parent.Parent.FullName;
		}

		if (entries.Any(x => x.Name.Equals(Constants.AppDirName, StringComparison.OrdinalIgnoreCase)))
		{
			// we are in "game/data/"
			return currentDir.Parent.FullName;
		}

		if (entries.Any(x => x.Name.Equals("table.vpp_pc", StringComparison.OrdinalIgnoreCase)))
		{
			// we are in "game/data/"
			return currentDir.Parent.FullName;
		}

		if (entries.Any(x => x.Name.Equals("data", StringComparison.OrdinalIgnoreCase)))
		{
			// we are in "game/"
			return currentDir.FullName;
		}

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\2029222893", false);
            var location = key.GetValue(@"path") as string;
            if (string.IsNullOrEmpty(location))
            {
                log.LogDebug($"GOG location not found in registry");
            }
            else
            {
                return location;
            }
        }
        catch (Exception ex)
        {
            log.LogDebug($"Could not autodetect GOG location: {ex.Message}");
        }

		try
		{

			using var key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam", false);
			var steamLocation = key.GetValue(@"InstallPath") as string;
			if (string.IsNullOrEmpty(steamLocation))
			{
				log.LogInformation("Is Steam installed?");
			}
            else
            {
                var config = await File.ReadAllTextAsync($@"{steamLocation}\steamapps\libraryfolders.vdf", token);
                var regex = new Regex(@"""path""\s+""(.+?)""");
                var locations = regex.Matches(config).Select(x => x.Groups).Select(x => x[1].Value)
                    .Select(x => x.Replace(@"\\", @"\").TrimEnd('\\'));
                var gamePath = @"steamapps\common\Red Faction Guerrilla Re-MARS-tered";
                foreach (var location in locations)
                {
                    log.LogDebug($"Trying library at `{location}`...");
                    var gameDir = Path.Combine(location, gamePath);
                    if (Directory.Exists(gameDir))
                    {
                        return gameDir;
                    }
                }
            }
		}
		catch (Exception ex)
		{
			log.LogDebug($"Could not autodetect Steam location: {ex.Message}");
		}
        log.LogInformation("Game is not found nearby, in Gog via registry, or in any of Steam libraries!");
        return string.Empty;

	}
}
