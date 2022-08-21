using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTMLConverter;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction.Services;

public class UiTools
{
    private readonly MarkdownRender render;
    private readonly Tools tools;
    private readonly FfClient ffClient;
    public readonly List<IMod> items = new();
    public long? newCommunityPatch;

    public UiTools(MarkdownRender render, Tools tools, FfClient ffClient)
    {
        this.render = render;
        this.tools = tools;
        this.ffClient = ffClient;
    }

    public async Task<bool> PopulateData(Filesystem filesystem, CancellationToken token)
    {
        // populate backup if none
        var backupSuccess = EnsureBackup(filesystem);
        if (backupSuccess == false)
        {
            return false;
        }

        // populate community patch info
        await CheckCommunityPatch(filesystem, token);
        if (newCommunityPatch != null)
        {
            // not null value means we need to update first, don't populate mods
            return true;
        }

        // populate mod list
        await Connect(token);
        return true;
    }
    
    public async Task ApplySelectedAndRun(Filesystem filesystem, IMod? mod, CancellationToken token)
    {
        var applied = await ApplySelected(filesystem, mod, token);
        if (!applied)
        {
            return;
        }

        render.Append("> Launching game via Steam...");
        Process.Start(new ProcessStartInfo()
        {
            UseShellExecute = true,
            FileName = "steam://rungameid/667720"
        });
    }
    
    public async Task<bool> ApplySelected(Filesystem filesystem, IMod? mod, CancellationToken token)
    {
        render.Clear();
        render.Append($"> Applying: {mod}...");

        if (mod is null or SeparatorItem)
        {
            render.Append("# Select mod from side panel to apply");
            return false;
        }

        return await ApplyMod(filesystem, mod, token);
    }

    public async Task DisplayMod(IMod? mod, CancellationToken token)
    {
        render.Clear();

        if (mod is SeparatorItem)
        {
            return;
        }

        render.Append($"> Displaying readme for {mod}...");
        if (mod is null)
        {
            throw new ArgumentNullException(nameof(mod));
        }

        render.Clear();
        render.Append(mod.Markdown, false);
    }

    /// <summary>
    /// Returns null if no action needed, or ID of latest patch to install
    /// </summary>
    public async Task CheckCommunityPatch(Filesystem filesystem, CancellationToken token)
    {
        render.Append("> Checking community patch...");
        var id = filesystem.GetInstalledCommunityPatchId();
        var humanReadableId = id == null ? "not installed" : id.ToString();
        render.Append($"Installed patch: **{humanReadableId}**");
        var latestId = await ffClient.GetLatestCommunityPatchId(token);
        render.Append($"Latest patch on FactionFiles: **{latestId}**");
        if (id != latestId)
        {
            var modUrl = string.Format(Constants.BrowserUrlTemplate, latestId);
            render.Append(@$"# You don't have latest community patch installed!

See changelog: [{modUrl}]({modUrl})

# What is this?

Multiplayer mods depend on community patch. Even some singleplayer mods too! **It is highly recommended to have latest version installed.**
This app is designed to keep players up-to-date without choices to avoid issues in multiplayer.
If you don't need this, install mods manually or suggest an improvement at GitHub or FF Discord.

# Press button below to update your game

Mod management will be available after updating
");
            newCommunityPatch = latestId;
        }
        else
        {
            newCommunityPatch = null;
        }
    }
    
    public async Task Restore(Filesystem filesystem, CancellationToken token)
    {
        render.Clear();
        render.Append("> Checking or creating backup...");
        var bakupSuccess = tools.EnsureBackup(filesystem, Enumerable.Empty<string>().ToList());
        if (!bakupSuccess)
        {
            throw new InvalidOperationException("Could not create backup!");
        }

        var remainingFiles = new HashSet<string>(Constants.AllKnownVppPc.Keys);
        render.Append("> Copying files from backup...");
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

            render.Append($"* {fileName}");
        }

        render.Append($"**Success!**");
    }

    public async Task Connect(CancellationToken token)
    {
        render.Append($"> Downloading map lists from FactionFiles...");
        items.Clear();

        // upd list
        await AddNonEmptyCategoryItems(Category.MapPacks, "▶ Map Packs ◀", token);
        await AddNonEmptyCategoryItems(Category.MpMaps, "▶ MP Maps ◀", token);
        await AddNonEmptyCategoryItems(Category.WcMaps, "▶ WC Maps ◀", token);
        
        items.Add(new SeparatorItem("▶ Mods ◀"));
        items.Add(new SeparatorItem("(not yet)"));

        // upd text
        var document = await ffClient.GetNewsWiki(token);
        var header = document.GetElementById("firstHeading").TextContent;
        var content = document.GetElementById("mw-content-text").InnerHtml;
        var xaml = HtmlToXamlConverter.ConvertHtmlToXaml(content, true);
        render.Clear();
        render.AppendXaml($"# {header}\n\n", xaml, false);
    }

    public async Task AddNonEmptyCategoryItems(Category category, string label, CancellationToken token)
    {
        var mods = await ffClient.GetMods(category, token);
        if (!mods.Any())
        {
            return;
        }
        items.Add(new SeparatorItem(label));
        items.AddRange(mods);
    }

    public async Task<string> Detect(CancellationToken token)
    {
        render.Append("> Looking for game install path...");
        var result = await tools.DetectGameLocation(token, s => render.Append(s));
        render.Append($"> Found game: `{result}`");
        return result;
    }

    public bool EnsureBackup(Filesystem filesystem)
    {
        return tools.EnsureBackup(filesystem, Enumerable.Empty<string>().ToList());
    }

    public async Task<bool> UpdateCommunityPatch(Filesystem filesystem, long latestCommunityPatch, CancellationToken token)
    {
        var mods = await ffClient.GetMods(Category.ModsStandalone, token);
        var latestPatch = mods.Single(x => x.Id == latestCommunityPatch);
        var success = await ApplyMod(filesystem, latestPatch, token);
        if (success)
        {
            filesystem.WriteCommunityPatchId(latestCommunityPatch);
            render.Append($"# Successfully updated game to community patch {latestPatch}!");
        }

        return success;
    }

    private async Task<bool> ApplyMod(Filesystem filesystem, IMod mod, CancellationToken token)
    {
        var modDir = filesystem.ModDir(mod);
        await ffClient.DownloadMod(filesystem, mod, token);
        render.Append("> Checking or creating backup...");
        var bakupSuccess = tools.EnsureBackup(filesystem, modDir.GetFiles().Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList());
        if (!bakupSuccess)
        {
            throw new InvalidOperationException("Could not create backup!");
        }

        var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
        render.Append($"> Mod contents: {files}");
        var success = await tools.ApplyMod(filesystem, mod, token);
        if (success)
        {
            render.Append($"**Success!**");
        }
        else
        {
            render.Append("Failed to apply mod");
        }

        return success;
    }
}
