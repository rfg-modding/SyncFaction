using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PleOps.XdeltaSharp.Decoder;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction.Services;

public class Tools
{
    private readonly MarkdownRender render;

    public Tools(MarkdownRender render)
    {
        this.render = render;
    }
    
    public async Task<bool> ApplyMod(Filesystem filesystem, IMod mod, CancellationToken token)
    {
        // TODO support other stuff like .exe in base game directory and other file types besides vpp_pc
        
        var modDir = filesystem.ModDir(mod);
        var modFiles = modDir.EnumerateFiles().ToList();
        
        // restore files from backup if they were backed up before
        // DO NOT restore files belonging to community patch
        // TODO community patch txt should list files and hashes? keep track and backup of community-patched files!
        throw new NotImplementedException("current method reverts community patch when applying another mod");
        var remainingFiles = new HashSet<string>(Constants.KnownMpVppPc.Keys);
        foreach (var x in remainingFiles)
        {
            var fileName = x + ".vpp_pc";
            var srcFile = new FileInfo(Path.Combine(filesystem.Bak.FullName, fileName));
            if (!srcFile.Exists)
            {
                // file is not backed up meaning it was not modified before
                continue;
            }

            var dstFile = new FileInfo(Path.Combine(filesystem.Data.FullName, fileName));
            if (dstFile.Exists)
            {
                dstFile.Delete();
            }

            srcFile.CopyTo(dstFile.FullName);
            render.Append($"* Copied original {fileName}");
        }

        bool anythingChanged = false;
        // process each file in mod dir
        foreach (var x in modFiles)
        {
            var item_name = Path.GetFileNameWithoutExtension(x.Name);
            var fileName = item_name + ".vpp_pc";
            var dstFile = new FileInfo(Path.Combine(filesystem.Data.FullName, fileName));
            
            if (Path.GetExtension(x.Name) == ".xdelta")
            {
                // apply binary patch
                if (dstFile.Exists)
                {
                    dstFile.Delete();
                }
                dstFile.Create().Close();
                await using var srcStream = new FileInfo(Path.Combine(filesystem.Bak.FullName, fileName)).OpenRead();
                await using var patchStream = x.OpenRead();
                await using var dstStream = dstFile.Open(FileMode.Create, FileAccess.ReadWrite);

                using var decoder = new Decoder(srcStream, patchStream, dstStream);
                decoder.Run();

                remainingFiles.Remove(item_name);
                render.Append($"* __Patched__ {fileName}");
                anythingChanged = true;
            }
            else if (Path.GetExtension(x.Name) == ".vpp_pc")
            {
                // TODO test!
                // copy file over original
                x.CopyTo(dstFile.FullName);
                render.Append($"* __Copied__ {fileName}");
                anythingChanged = true;
            }
            else
            {
                render.Append($"* Skipped ~~{x.Name}~~");
            }
        }

        if (!anythingChanged)
        {
            render.Append("# Nothing was changed by mod, maybe it contained only unsupported files");
        }

        return anythingChanged;
    }

    public bool EnsureBackup(Filesystem filesystem, IReadOnlyList<string> filesToBackUp)
    {
        if (!filesystem.Bak.Exists)
        {
            // probably is't first launch, check all files and bail if something is wrong
            render.Append($"# Validating game contents. This is one-time thing, but going to take a while");
            foreach (var kv in Constants.AllKnownVppPc.OrderBy(x => x.Key))
            {
                // TODO support other files, move extension to KnownFiles table
                var key = $"{kv.Key}.vpp_pc";
                var file = filesystem.Data.EnumerateFiles().Single(x => x.Name == key);
                render.Append($"* *Checking* {file.Name}");
                if (!CheckHash(file, kv.Value))
                {
                    render.Append(@$"# Action needed:

Found modified game file: {file.FullName}

Looks like you've installed some mods before. SyncFaction can't work until you restore all files to their default state.

**Use Steam to verify integrity of game files and let it download original data. Then run SyncFaction again.**

*See you later miner!*
");
                    return false;
                }
            }


            filesystem.Bak.Create();
        }

        var existingFiles = filesystem.Bak.GetFiles();
        var existingMapFiles = existingFiles
            .Where(x => x.Extension == ".vpp_pc")
            .Select(x => Path.GetFileNameWithoutExtension(x.Name))
            .ToList();

        var filesToCopy = filesToBackUp.Intersect(Constants.AllKnownVppPc.Keys).Except(existingMapFiles);
        // dont check hashes if backup has a file (it's slow). probably user didn't fiddle with our directory
        if (filesToCopy.Any())
        {
            render.Append($"Backing up files: {filesToCopy}");
            // backup is compromised, erase and copy over
            var dataFiles = filesystem.Data.GetFiles();
            foreach (var x in filesToCopy)
            {
                var name = $"{x}.vpp_pc";
                var file = existingFiles.SingleOrDefault(x => x.Name.ToLowerInvariant() == name);
                file?.Delete();
                var source = dataFiles.Single(x => x.Name.ToLowerInvariant() == name);
                if (!CheckHash(source, Constants.AllKnownVppPc[x]))
                {
                    throw new InvalidOperationException(
                        $"Found modified game file, can't back up! {source.FullName}");
                }

                var destination = Path.Combine(filesystem.Bak.FullName, name);
                source.CopyTo(destination);
                render.Append($"Copied to backup: {source.FullName}");
            }
        }

        return true;
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
            render.Append($"> Bad hash. Expected {expected}, got {hash}");
        }

        return result;
    }

    public async Task<string> DetectGameLocation(CancellationToken token, Action<string> callback = null)
    {
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
        return Constants.AllKnownVppPc.ContainsKey(name);
    }
}
