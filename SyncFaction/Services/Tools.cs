using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PleOps.XdeltaSharp.Decoder;

namespace SyncFaction.Services;

public class Tools
{
    private readonly ILogger<Tools> log;

    public Tools(ILogger<Tools> log)
    {
        this.log = log;
    }

    public async Task<bool> ApplyMod(DirectoryInfo dataDir, DirectoryInfo bakDir, DirectoryInfo modDir,
        CancellationToken token, Action<string> callback = null)
    {
        var modFiles = modDir.EnumerateFiles().ToList();
        if (!modFiles.Any(x => Path.GetExtension(x.Name) == ".xdelta"))
        {
            callback?.Invoke("# Mod does not contain .xdelta patches and is not supported");
            return false;
        }

        var remainingFiles = new HashSet<string>(Constants.KnownFiles.Keys);
        // restore files from backup if they were backed up before
        foreach (var x in remainingFiles)
        {
            var fileName = x + ".vpp_pc";
            var srcFile = new FileInfo(Path.Combine(bakDir.FullName, fileName));
            if (!srcFile.Exists)
            {
                // file is not backed up meaning it was not modified before
                continue;
            }

            var dstFile = new FileInfo(Path.Combine(dataDir.FullName, fileName));
            if (dstFile.Exists)
            {
                dstFile.Delete();
            }

            srcFile.CopyTo(dstFile.FullName);
            log.LogInformation("Copied vanilla file {file}", fileName);
            callback?.Invoke($"* Copied original {fileName}");
        }

        // apply patches for each file in mod dir
        foreach (var x in modFiles)
        {
            if (Path.GetExtension(x.Name) != ".xdelta")
            {
                callback?.Invoke($"* Skipped ~~{x.Name}~~");
                continue;
            }

            var item_name = Path.GetFileNameWithoutExtension(x.Name);
            var fileName = item_name + ".vpp_pc";
            log.LogInformation("Patching {file}", fileName);

            var dstFile = new FileInfo(Path.Combine(dataDir.FullName, fileName));
            if (dstFile.Exists)
            {
                dstFile.Delete();
            }

            dstFile.Create().Close();

            await using var srcStream = new FileInfo(Path.Combine(bakDir.FullName, fileName)).OpenRead();
            await using var patchStream = x.OpenRead();
            await using var dstStream = dstFile.Open(FileMode.Create, FileAccess.ReadWrite);

            using var decoder = new Decoder(srcStream, patchStream, dstStream);
            decoder.Run();

            remainingFiles.Remove(item_name);
            log.LogInformation("Patched file {file}", fileName);
            callback?.Invoke($"* __Patched__ {fileName}");
        }

        return true;
    }

    public DirectoryInfo? EnsureBackup(DirectoryInfo dataDir, IReadOnlyList<string> filesToBackUp,
        Action<string> callback = null)
    {
        var appDir = new DirectoryInfo(Path.Combine(dataDir.FullName, Constants.appDirName));
        var bakDir = new DirectoryInfo(Path.Combine(appDir.FullName, Constants.bakDirName));
        if (!bakDir.Exists)
        {
            // probably is't first launch, check all files and bail if something is wrong
            callback?.Invoke($"# Validating game contents. This is one-time thing, but going to take a while");
            foreach (var kv in Constants.KnownFiles.OrderBy(x => x.Key))
            {
                var file = dataDir.EnumerateFiles().Single(x => Path.GetFileNameWithoutExtension(x.Name) == kv.Key);
                callback?.Invoke($"* *Checking* {file.Name}");
                if (!CheckHash(file, kv.Value))
                {
                    callback?.Invoke(@$"# Action needed:

Found modified game file: {file.FullName}

Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.

**Use Steam to verify integrity of game files and let it download original data. Then run SyncFaction again.**

*See you later miner!*
");
                    return null;
                }
            }


            bakDir.Create();
        }

        var existingFiles = bakDir.GetFiles();
        var existingMapFiles = existingFiles
            .Where(x => x.Extension == ".vpp_pc")
            .Select(x => Path.GetFileNameWithoutExtension(x.Name))
            .ToList();

        var filesToCopy = filesToBackUp.Intersect(Constants.KnownFiles.Keys).Except(existingMapFiles);
        // dont check hashes if backup has a file (it's slow). probably user didn't fiddle with our directory
        if (filesToCopy.Any())
        {
            log.LogWarning("Backing up files: {files}", filesToCopy);
            // backup is compromised, erase and copy over
            var dataFiles = dataDir.GetFiles();
            foreach (var x in filesToCopy)
            {
                var name = $"{x}.vpp_pc";
                var file = existingFiles.SingleOrDefault(x => x.Name.ToLowerInvariant() == name);
                file?.Delete();
                var source = dataFiles.Single(x => x.Name.ToLowerInvariant() == name);
                if (!CheckHash(source, Constants.KnownFiles[x]))
                {
                    throw new InvalidOperationException(
                        $"Found modified game file, can't back up! {source.FullName}");
                }

                var destination = Path.Combine(bakDir.FullName, name);
                source.CopyTo(destination);
                log.LogWarning("Copied to backup: {file}", source.FullName);
            }
        }

        return bakDir;
    }

    public bool CheckHash(FileInfo file, string expected)
    {
        using var sha = SHA256.Create();
        using var fileStream = file.Open(FileMode.Open);
        fileStream.Position = 0;
        var hashValue = sha.ComputeHash(fileStream);
        //var hash = Convert.ToHexString(hashValue);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");

        var result = expected.Equals(hash, StringComparison.OrdinalIgnoreCase);
        if (!result)
        {
            log.LogWarning("Bad hash. Expected {expectedHash}, got {hash}", expected, hash);
        }

        return result;
    }

    public async Task<string> DetectGameLocation(CancellationToken token, Action<string> callback = null)
    {
        //"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\edi_wang"))
        {
            if (key != null)
            {
                Object o = key.GetValue("Title");
                Console.WriteLine(o.ToString());
            }
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam", false);
            var steamLocation = key.GetValue(@"InstallPath") as string;
            if (string.IsNullOrEmpty(steamLocation))
            {
                throw new InvalidOperationException("Is Steam installed?");
            }

            var config = await File.ReadAllTextAsync($@"{steamLocation}\steamapps\libraryfolders.vdf", token);
            var regex = new Regex(@"""path""\s+""(.+?)""");
            var locations = regex.Matches(config).Select(x => x.Groups).Select(x => x[1].Value)
                .Select(x => x.Replace(@"\\", @"\").TrimEnd('\\'));
            var gamePath = @"steamapps\common\Red Faction Guerrilla Re-MARS-tered";
            foreach (var location in locations)
            {
                callback?.Invoke($"> Trying library at `{location}`...");
                var gameDir = Path.Combine(location, gamePath);
                if (Directory.Exists(gameDir))
                {
                    return gameDir;
                }
            }

            callback?.Invoke("> Game not installed in any of Steam libraries!");
            return string.Empty;
        }
        catch (Exception ex)
        {
            callback?.Invoke($"> Could not detect game: {ex.Message}");
            return string.Empty;
        }
    }

    public bool IsKnown(string value)
    {
        var name = Path.GetFileNameWithoutExtension(value.ToLowerInvariant());
        return Constants.KnownFiles.ContainsKey(name);
    }
}