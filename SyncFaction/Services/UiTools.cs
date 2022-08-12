using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTMLConverter;
using SyncFaction.Services;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction;

public class UiTools
{
    private readonly MarkdownRender render;
    private readonly Tools tools;
    private readonly FfClient ffClient;
    public readonly List<IMod> items = new();

    public UiTools(MarkdownRender render, Tools tools, FfClient ffClient)
    {
        this.render = render;
        this.tools = tools;
        this.ffClient = ffClient;
    }

    public async Task<bool> ApplySelected(DirectoryInfo gameDir, IMod mod, CancellationToken token)
    {
        render.Clear();
        render.Append($"> Applying: {mod}...");

        if (mod is null or SeparatorItem)
        {
            render.Append("> Select mod from side panel to apply");
            return false;
        }

        var dataDir = gameDir.GetDirectories().Single(x => x.Name == "data");
        var appDir = new DirectoryInfo(Path.Combine(dataDir.FullName, Constants.appDirName));
        var modDir = await ffClient.DownloadMod(appDir, mod, token);
        var files = string.Join(", ", modDir.GetFiles().Select(x => $"`{x.Name}`"));
        render.Append("> Checking or creating backup...");
        var bakDir = tools.EnsureBackup(dataDir,
            modDir.GetFiles().Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList());
        if (bakDir == null)
        {
            throw new InvalidOperationException("Could not create backup!");
        }

        render.Append($"> Mod contents: {files}");
        var success = await tools.ApplyMod(dataDir, bakDir, modDir, token, s => render.Append(s));
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

    public async Task DisplayMod(IMod mod, CancellationToken token)
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

    public async Task Restore(DirectoryInfo gameDir, CancellationToken token)
    {
        render.Clear();
        var dataDir = gameDir.GetDirectories().Single(x => x.Name == "data");
        var appDir = new DirectoryInfo(Path.Combine(dataDir.FullName, Constants.appDirName));
        render.Append("> Checking or creating backup...");
        var bakDir = tools.EnsureBackup(dataDir, Enumerable.Empty<string>().ToList());
        if (bakDir == null)
        {
            throw new InvalidOperationException("Could not create backup!");
        }

        var remainingFiles = new HashSet<string>(Constants.KnownFiles.Keys);
        render.Append("> Copying files from backup...");
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

    public DirectoryInfo? EnsureBackup(DirectoryInfo gameDir)
    {
        var dataDir = gameDir.GetDirectories().Single(x => x.Name == "data");
        return tools.EnsureBackup(dataDir, Enumerable.Empty<string>().ToList(), s => render.Append(s));
    }
}
