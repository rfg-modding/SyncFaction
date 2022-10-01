using System.Diagnostics;
using System.Text.Json;
using HTMLConverter;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Services;

public class UiCommands
{
    private readonly ILogger log;
    private readonly FfClient ffClient;
    private readonly FileManager fileManager;
    private readonly StateProvider stateProvider;
    public readonly List<IMod> items = new();
    public bool newCommunityVersion;
    private long patchId;
    private List<long>? updateIds;

    public UiCommands(ILogger<UiCommands> log, FfClient ffClient, FileManager fileManager, StateProvider stateProvider)
    {
        this.log = log;
        this.ffClient = ffClient;
        this.fileManager = fileManager;
        this.stateProvider = stateProvider;
    }

    public async Task<bool> PopulateData(IStorage storage, CancellationToken token)
    {
        // create dirs and validate files if required
        var success = fileManager.DoFirstLaunchCheck(storage);
        if (success == false)
        {
            return false;
        }

        // populate community patch info
        await CheckCommunityUpdates(storage, token);
        if (newCommunityVersion)
        {
            // we need to update first, don't populate mods
            return true;
        }

        // populate mod list
        await Connect(token);
        return true;
    }

    public async Task ApplySelectedAndRun(IStorage storage, IMod? mod, CancellationToken token)
    {
        var applied = await ApplySelected(storage, mod, token);
        if (!applied)
        {
            return;
        }

        log.LogInformation("Launching game via Steam...");
        Process.Start(new ProcessStartInfo()
        {
            UseShellExecute = true,
            FileName = "steam://rungameid/667720"
        });
    }

    public async Task<bool> ApplySelected(IStorage storage, IMod? mod, CancellationToken token)
    {
        log.Clear();
        log.LogDebug($"Applying: {mod}...");

        if (mod is null or SeparatorItem)
        {
            log.LogWarning("Select mod from side panel to apply");
            return false;
        }

        return await InstallMod(storage, mod, token);
    }

    public async Task DisplayMod(IMod? mod, CancellationToken token)
    {
        log.Clear();

        if (mod is SeparatorItem)
        {
            return;
        }

        log.LogDebug($"Displaying readme for {mod}...");
        if (mod is null)
        {
            throw new ArgumentNullException(nameof(mod));
        }

        log.Clear();
        log.LogInformation(new EventId(0, "log_false"), mod.Markdown);
    }

    public async Task CheckCommunityUpdates(IStorage storage, CancellationToken token)
    {
        log.LogInformation("Reading current state...");
        stateProvider.State = storage.LoadState() ?? new State();
        log.LogInformation($"Installed community patch and updates: **{stateProvider.State.GetHumanReadableCommunityVersion()}**");
        patchId = await ffClient.GetCommunityPatchId(token);
        updateIds = await ffClient.GetCommunityUpdateIds(token);
        if (stateProvider.State.CommunityPatch != patchId || !stateProvider.State.CommunityUpdates.SequenceEqual(updateIds))
        {
            log.LogWarning(@$"You don't have latest community patch installed!

# What is this?

Multiplayer mods depend on community patch and its updates. Even some singleplayer mods too! **It is highly recommended to have latest versions installed.**
This app is designed to keep players updated to avoid issues in multiplayer.
If you don't need this, install mods manually, suggest an improvement at Github or FF Discord, or enable dev mode.

# Press button below to update your game

Mod management will be available after updating.

Changelogs and info:
");
            log.LogInformation($"* [Community patch base (id {patchId})]({FormatUrl(patchId)})");
            var i = 1;
            foreach (var update in updateIds)
            {
                log.LogInformation($"* [Community patch update {i} (id {update})]({FormatUrl(update)})");
                i++;
            }
            newCommunityVersion = true;
        }
        else
        {
            newCommunityVersion = false;
        }
        string FormatUrl(long x) => string.Format(Constants.BrowserUrlTemplate, x);
    }

    public async Task Restore(IStorage storage, bool toVanilla, CancellationToken token)
    {
        log.Clear();
        await fileManager.Restore(storage, toVanilla, token);
        if (toVanilla)
        {
            // forget we had updates
            stateProvider.State.CommunityPatch = 0;
            stateProvider.State.CommunityUpdates.Clear();
            storage.WriteState(stateProvider.State);
        }
    }

    public async Task Connect(CancellationToken token)
    {
        log.LogDebug($"Downloading map lists from FactionFiles...");
        items.Clear();

        // upd list
        await AddNonEmptyCategoryItems(Category.MapPacks, "▷ Map Packs ◁", token);
        await AddNonEmptyCategoryItems(Category.MpMaps, "▷ MP Maps ◁", token);
        await AddNonEmptyCategoryItems(Category.WcMaps, "▷ WC Maps ◁", token);

        items.Add(new SeparatorItem("▷ Mods ◁"));
        items.Add(new SeparatorItem("(not yet)"));

        // upd text
        var document = await ffClient.GetNewsWiki(token);
        var header = document.GetElementById("firstHeading").TextContent;
        var content = document.GetElementById("mw-content-text").InnerHtml;
        var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
        log.Clear();
        log.LogInformation(new EventId(0, "log_false"), $"# {header}\n\n");
        log.LogInformationXaml(xaml, false);

        async Task AddNonEmptyCategoryItems(Category category, string label, CancellationToken token)
        {
            var mods = await ffClient.GetMods(category, token);
            if (!mods.Any())
            {
                return;
            }
            items.Add(new SeparatorItem(label));
            items.AddRange(mods);
        }
    }

    public async Task<string> Detect(CancellationToken token)
    {
        log.LogDebug("Looking for game install path...");
        var result = await Storage.DetectGameLocation(log, token);
        if (result == string.Empty)
        {
            log.LogError("Unable to autodetect game location! Is it GOG version?");
        }
        return result;
    }

    public async Task<bool> UpdateCommunityPatch(IStorage storage, CancellationToken token)
    {
        if (!newCommunityVersion || patchId == 0 || updateIds == null)
        {
            log.LogError($"Install community patch failed. please contact developer. `newCommunityVersion=[{newCommunityVersion}], patch=[{patchId}], update count=[{updateIds?.Count}]`");
            return false;
        }

        var mods = await ffClient.GetMods(Category.ModsStandalone, token);
        var patch = mods.Single(x => x.Id == patchId);
        var updates = updateIds.Select(x => mods.Single(y => y.Id == x)).ToList();

        var result = await UpdateInternal(patch, updates, storage, token);
        if (!result)
        {
            log.LogError(@$"Action needed:

Failed to update game to latest community patch.

SyncFaction can't work until you restore all files to their default state.

**Use Steam to verify integrity of game files and let it download original data. Then run SyncFaction again.**

*See you later miner!*
");
            return false;
        }

        stateProvider.State.CommunityPatch = patchId;
        stateProvider.State.CommunityUpdates = updateIds;
        storage.WriteState(stateProvider.State);
        log.LogWarning($"Successfully updated game to community patch: **{stateProvider.State.GetHumanReadableCommunityVersion()}**");
        return true;
    }

    private async Task<bool> UpdateInternal(Mod patch, IEnumerable<Mod> updates, IStorage storage, CancellationToken token)
    {
        if (stateProvider.State.CommunityPatch != patch.Id)
        {
            var modDir = storage.GetModDir(patch);
            var successDl = await ffClient.DownloadAndUnpackMod(modDir, patch, token);
            if (!successDl)
            {
                return false;
            }
            var successPatch = await fileManager.InstallCommunityPatchBase(storage, patch, token);
            if (!successPatch)
            {
                return false;
            }
            stateProvider.State.CommunityUpdates.Clear();
        }

        if (!updateIds.SequenceEqual(stateProvider.State.CommunityUpdates))
        {
            var installed = stateProvider.State.CommunityUpdates.ToList();
            var pendingUpdates = updates.ToList();
            while (installed.Any())
            {
                var current = installed.First();
                var apiUpdate = pendingUpdates.First();
                if (current != apiUpdate.Id)
                {
                    log.LogError($"Updates are mixed up, please contact developer");
                    log.LogInformation($"`state={JsonSerializer.Serialize(stateProvider.State.CommunityUpdates)} updates={JsonSerializer.Serialize(updateIds)}`");
                    return false;
                }
                installed.RemoveAt(0);
                pendingUpdates.RemoveAt(0);
            }
            log.LogDebug($"Updates to install: {JsonSerializer.Serialize(pendingUpdates.Select(x => x.Id))}");
            foreach (var update in pendingUpdates)
            {
                var updDir = storage.GetModDir(update);
                var success = await ffClient.DownloadAndUnpackMod(updDir, update, token);
                if (!success)
                {
                    return false;
                }
            }
            var result = await fileManager.InstallCommunityUpdateIncremental(storage, pendingUpdates, token);
            if (!result)
            {
                log.LogError($"Update community patch failed. please contact developer. `newCommunityVersion=[{newCommunityVersion}], patch=[{patchId}], update count=[{updateIds?.Count}]`");
            }

            return result;
        }

        return true;

    }

    /// <summary>
    /// Returns list of relative paths to modified files, eg. "data/foo.vpp_pc"
    /// </summary>
    private async Task<bool> InstallMod(IStorage storage, IMod mod, CancellationToken token)
    {
        var modDir = storage.GetModDir(mod);
        await ffClient.DownloadAndUnpackMod(modDir, mod, token);

        var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
        log.LogDebug($"Mod contents: {files}");
        var success = await fileManager.InstallModExclusive(storage, mod, token);
        if (success)
        {
            log.LogInformation($"**Success!**");
        }
        else
        {
            log.LogInformation("Failed to apply mod");
        }

        return success;
    }

    public void ToggleDevMode(IStorage filesystem, bool devModeIsChecked)
    {
        stateProvider.State.DevMode = devModeIsChecked;
        filesystem.WriteState(stateProvider.State);
        var text = stateProvider.State.DevMode ? "enabled" : "disabled";
        log.LogWarning($"Dev mode **{text}**");
    }
}
