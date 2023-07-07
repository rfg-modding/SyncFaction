using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastHashes;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.Services;

public class ModLoader
{
    private readonly HttpClient client;
    private readonly IFileSystem fileSystem;
    private readonly FfClient ffClient;
    private readonly ILogger<ModLoader> log;

    public ModLoader(HttpClient client, IFileSystem fileSystem, FfClient ffClient, ILogger<ModLoader> log)
    {
        this.client = client;
        this.fileSystem = fileSystem;
        this.ffClient = ffClient;
        this.log = log;
    }

    internal async Task<IReadOnlyList<IMod>> GetMods(Category category, IGameStorage storage, CancellationToken token)
    {
        var mods = category switch
        {
            Category.Local => GetLocalMods(storage),
            Category.Dev => await GetDevMods(token),
            _ => await ffClient.GetFfMods(category, storage, token)
        };
        foreach (var mod in mods)
        {
            mod.Status = GetModStatus(mod, storage);
            log.LogTrace("Mod [{id}] status [{status}]", mod.Id, mod.Status);
        }

        return mods;
    }

    private List<LocalMod> GetLocalMods(IAppStorage storage)
    {
        log.LogInformation("Reading local mods in [{dir}]", storage.App.FullName);
        List<LocalMod> mods = new();
        foreach (var dir in storage.App.EnumerateDirectories().Where(static dir => !dir.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase)))
        {
            if (dir.Name.StartsWith("Mod_", StringComparison.OrdinalIgnoreCase))
            {
                // skip downloaded mods
                continue;
            }

            var id = BitConverter.ToInt64(new MurmurHash64().ComputeHash(Encoding.UTF8.GetBytes(dir.Name.ToLowerInvariant())));
            var mod = new LocalMod
            {
                Id = id,
                Name = dir.Name,
                Size = 0,
                DownloadUrl = null,
                ImageUrl = null,
                Status = OnlineModStatus.Ready
            };
            log.LogTrace("Mod in [{dir}]: added local mod, id {id}", dir.Name, mod.Id);
            mods.Add(mod);
        }

        return mods;
    }

    private async Task<IReadOnlyList<IMod>> GetDevMods(CancellationToken token)
    {
        // TODO: any pagination?
        log.LogInformation("Reading CDN for dev mods");
        var url = new UriBuilder(Constants.CdnListUrl) { Query = $"AccessKey={Constants.CdnReadApiKey}" }.Uri;

        log.LogTrace("Request: GET {url}", url.AbsolutePath);
        var response = await client.GetAsync(url, token);
        log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);
        await using var content = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);

        var data = JsonSerializer.Deserialize<List<CdnEntry>>(content);
        if (data == null)
        {
            throw new InvalidOperationException("CDN API returned unexpected data");
        }

        var result = new List<IMod>();
        foreach (var entry in data)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(entry.ObjectName)))
            {
                // skip files cached from FF
                continue;
            }

            if (entry.IsDirectory)
            {
                // skip directories (may be useful in the future)
                continue;
            }

            var mod = entry.ToMod();
            log.LogTrace("Mod [{dir}]: added dev mod, id {id}", mod.Name, mod.Id);
            result.Add(mod);
        }

        return result.OrderByDescending(x => x.CreatedAt).ToList();
    }

    private OnlineModStatus GetModStatus(IMod item, IGameStorage storage)
    {
        if (item is LocalMod)
        {
            return OnlineModStatus.Ready;
        }

        var modDir = storage.GetModDir(item);
        var incompleteDataFile = fileSystem.FileInfo.New(Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        var descriptionFile = fileSystem.FileInfo.New(Path.Combine(modDir.FullName, Constants.ModDescriptionFile));
        if (modDir.Exists && !incompleteDataFile.Exists && descriptionFile.Exists)
        {
            return OnlineModStatus.Ready;
        }

        return OnlineModStatus.None;
    }

    internal async Task<List<IMod>> GetAvailableMods(Settings settings, bool devMode, IGameStorage storage, CancellationToken token)
    {
        var result = new List<IMod>();
        foreach (var mod in EnumerateModFolders(storage, devMode))
        {
            mod.ModInfo = await ReadModInfo(mod, settings, storage, token);
            mod.Flags = GetFlags(mod, storage);
            result.Add(mod);
        }

        return result;
    }

    private async Task<ModInfo?> ReadModInfo(IMod mod, Settings settings, IGameStorage storage, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        log.LogTrace("Mod [{id}] in [{dir}]: reading modinfo.xml", mod.Id, modDir.Name);
        var xmlFile = modDir.EnumerateFiles("modinfo.xml", SearchOption.AllDirectories).FirstOrDefault();
        if (xmlFile is null)
        {
            log.LogTrace("Mod [{id}]: modinfo.xml not found", mod.Id);
            return null;
        }

        token.ThrowIfCancellationRequested();
        log.LogTrace("Mod [{id}]: modinfo.xml found", mod.Id);
        await using var s = xmlFile.OpenRead();
        var modInfo = ModInfo.LoadFromXml(s, xmlFile.Directory!, log);
        modInfo.CopySameOptions();
        if (settings.Mods.TryGetValue(mod.Id, out var modSettings))
        {
            log.LogTrace("Mod [{id}]: modinfo.xml has saved settings in state, loading", mod.Id);
            modInfo.LoadSettings(modSettings);
        }

        return modInfo;
    }

    private IEnumerable<IMod> EnumerateModFolders(IAppStorage storage, bool devMode)
    {
        log.LogTrace("Reading mods in [{dir}]", storage.App.FullName);
        foreach (var dir in storage.App.EnumerateDirectories().Where(static dir => !dir.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase)))
        {
            log.LogTrace("Mod in [{dir}]: trying", dir.Name);
            if (dir.Name.StartsWith("Mod_", StringComparison.OrdinalIgnoreCase))
            {
                // read mod description from json
                var descriptionFile = fileSystem.FileInfo.New(Path.Combine(dir.FullName, Constants.ModDescriptionFile));
                if (!descriptionFile.Exists)
                {
                    log.LogTrace("Mod in [{dir}] ignored: missing [{file}], probably was interrupted during download/unpack", dir.Name, descriptionFile.Name);
                    continue;
                }

                using (var reader = descriptionFile.OpenText())
                {
                    var json = reader.ReadToEnd();
                    var mod = JsonSerializer.Deserialize<Mod>(json);
                    if (mod.Hide && !devMode)
                    {
                        log.LogTrace("Mod in [{dir}] ignored: has hide flag in description and DevMode is not enabled", dir.Name);
                        continue;
                    }

                    log.LogTrace("Mod in [{dir}]: added downloaded mod, id {id}", dir.Name, mod.Id);
                    yield return mod;
                }
            }
            else
            {
                var id = BitConverter.ToInt64(new MurmurHash64().ComputeHash(Encoding.UTF8.GetBytes(dir.Name.ToLowerInvariant())));
                var mod = new LocalMod
                {
                    Id = id,
                    Name = dir.Name,
                    Size = 0,
                    DownloadUrl = string.Empty,
                    ImageUrl = null,
                    Status = OnlineModStatus.Ready
                };
                log.LogTrace("Mod in [{dir}]: added local mod, id {id}", dir.Name, mod.Id);
                yield return mod;
            }
        }
    }

    private ModFlags GetFlags(IMod mod, IGameStorage storage)
    {
        var modDir = storage.GetModDir(mod);
        log.LogTrace("Mod [{id}] in [{dir}]: reading files", mod.Id, modDir.Name);
        var modFiles = modDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(static x => !x.Name.StartsWith(".mod", StringComparison.OrdinalIgnoreCase));
        var flags = ModFlags.None;

        // NOTE: setting each flag only once to avoid log spam
        foreach (var modFile in modFiles)
        {
            if (!modFile.IsModContent())
            {
                log.LogTrace("Mod [{id}]: Ignored file [{file}]", mod.Id, modFile.FullName);
                continue;
            }

            var extension = modFile.Extension.ToLowerInvariant();
            var name = modFile.Name.ToLowerInvariant();
            if (!flags.HasFlag(ModFlags.HasXDelta) && extension is ".xdelta")
            {
                log.LogTrace("Mod [{id}]: Has XDelta [{file}]", mod.Id, modFile.FullName);
                flags |= ModFlags.HasXDelta;
            }
            else if(!flags.HasFlag(ModFlags.HasReplacementFiles))
            {
                log.LogTrace("Mod [{id}]: Has replacement or new file [{file}]", mod.Id, modFile.FullName);
                flags |= ModFlags.HasReplacementFiles;
            }

            // detecting if there are mp_file.vpp or mp_file.xdelta
            var nameNoExt = Path.GetFileNameWithoutExtension(name) + ".";
            if (!flags.HasFlag(ModFlags.AffectsMultiplayerFiles) && Hashes.MultiplayerFiles.Any(x => x.StartsWith(nameNoExt, StringComparison.OrdinalIgnoreCase)))
            {
                log.LogTrace("Mod [{id}]: Affects multiplayer [{file}]", mod.Id, modFile.FullName);
                flags |= ModFlags.AffectsMultiplayerFiles;
            }
        }

        // NOTE: modinfo must be already loaded
        if (mod.ModInfo is not null)
        {
            flags |= ModFlags.HasModInfo;
            // NOTE: this may not work properly if user inputs mention different vpps depending on selected values
            mod.ModInfo.ApplyUserInput();
            var archive = mod.ModInfo.GetPaths(storage.FileSystem, mod.ModInfo.TypedChanges.First().File).Archive;
            var nameNoExt = Path.GetFileNameWithoutExtension(archive) + ".";
            if (Hashes.MultiplayerFiles.Any(x => x.StartsWith(nameNoExt, StringComparison.OrdinalIgnoreCase)))
            {
                log.LogTrace("Mod [{id}]: modinfo.xml probably affects multiplayer [{file}]", mod.Id, archive);
                flags |= ModFlags.AffectsMultiplayerFiles;
            }
        }

        return flags;
    }
}
