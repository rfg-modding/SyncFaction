using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using SyncFaction.Services;

namespace SyncFaction;

public class UiTools
{
    private readonly MarkdownRender render;
    private readonly Tools tools;
    public List<Link> playlists = new ();

    public UiTools(MarkdownRender render, Tools tools)
    {
        this.render = render;
        this.tools = tools;
    }

    public async Task<bool> ApplyPlaylist(DirectoryInfo gameDir, string? playlistName, CancellationToken token)
    {
        render.Clear();
        render.Append($"> Applying playlist: {playlistName}...");

        if (playlistName == null)
        {
            render.Append("> Select playlist from side panel to apply");
            return false;
        }
        var playlist = playlists.SingleOrDefault(x => x.Name.StartsWith(playlistName));
        if (playlist is null)
        {
            throw new InvalidOperationException($"No playlist for given name [{playlistName}]. Playlists: {JsonSerializer.Serialize(playlists)}");
        }

        var dataDir = gameDir.GetDirectories().Single(x => x.Name == "data");
        var appDir = new DirectoryInfo(Path.Combine(dataDir.FullName, Tools.appDirName));
        var playlistDir = await tools.DownloadPlaylist(appDir, playlist, token);
        var patches = string.Join(", ", playlistDir.GetFiles().Select(x => $"`{x.Name}`"));

        render.Append("> Checking or creating backup...");
        var bakDir = tools.EnsureBackup(dataDir, playlistDir.GetFiles().Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList());

        render.Append($"> Patches to apply: {patches}");
        await tools.ApplyPlaylist(dataDir, bakDir, playlistDir, token, s => render.Append(s));
        render.Append($"**Success!**");
        playlistDir.Delete(true);
        return true;
    }
    public async Task GetReadme(string? playlistName, CancellationToken token)
    {
        render.Clear();
        render.Append($"> Downloading readme for {playlistName}...");
        if (playlistName is null)
        {
            throw new ArgumentNullException(nameof(playlistName));
        }
        var playlist = playlists.SingleOrDefault(x => x.Name.StartsWith(playlistName));
        if (playlist is null)
        {
            throw new InvalidOperationException($"No playlist for given name [{playlistName}]. Playlists: {JsonSerializer.Serialize(playlists)}");
        }

        var readme = await tools.GetReadme(playlist, token);
        render.Clear();
        render.Append(readme, false);
    }
    public async Task Restore(DirectoryInfo gameDir, CancellationToken token)
    {
        render.Clear();
        var dataDir = gameDir.GetDirectories().Single(x => x.Name == "data");
        var appDir = new DirectoryInfo(Path.Combine(dataDir.FullName, Tools.appDirName));
        render.Append("> Checking or creating backup...");
        var bakDir = tools.EnsureBackup(dataDir, Enumerable.Empty<string>().ToList());
        var remainingFiles = new HashSet<string>(tools.knownFiles.Keys);
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
    public async Task Connect(string url, CancellationToken token)
    {
        render.Append($"> Downloading map lists from {url}...");

        // upd text
        var allLinks = await tools.GetIndexLinks(url, token);
        var news = await tools.GetNews(allLinks, token);

        render.Clear();
        render.Append(news, false);

        // upd list
        playlists = tools.GetPlaylists(allLinks);

    }
    public async Task<string> Detect(CancellationToken token)
    {
        render.Append("> Looking for game install path...");
        var result = await tools.DetectGameLocation(token, s => render.Append(s));
        render.Append($"> Found game: `{result}`");
        return result;
    }
}
