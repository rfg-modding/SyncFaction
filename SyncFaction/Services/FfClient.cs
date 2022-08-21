using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using JorgeSerrano.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction.Services;

public class FfClient
{
    private readonly HttpClient client;
    private readonly MarkdownRender render;

    public FfClient(HttpClient client, MarkdownRender render)
    {
        this.client = client;
        this.render = render;
    }

    public async Task<List<Mod>> GetMods(Category category, CancellationToken token)
    {
        // NOTE: pagination currently is not implemented, everything is returned on first page
        render.Append($"> Reading FactionFiles category: {category}");
        var builder = new UriBuilder(Constants.ApiUrl);
        builder.Query = $"cat={category:D}&page=1";
        var url = builder.Uri;

        var response = await client.GetAsync(url, token);
        await using var content = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);

        var data = DeserializeData(content);
        if (data == null)
        {
            throw new InvalidOperationException("FactionFiles API returned unexpected data");
        }

        if (data.ResultsTotal != data.Results.Count)
        {
            throw new InvalidOperationException("FffactionFiles API returned partial data, app update required to support this!");
        }

        foreach (var item in data.Results.Values)
        {
            item.DescriptionMd = BbCodeToMarkdown(item.Description);

            if (string.IsNullOrEmpty(item.ImageThumb4By3Url))
            {
                item.ImageThumb4By3Url = null;
                continue;
            }

            var image = await client.GetAsync(item.ImageThumb4By3Url, token);
            await using var stream = await image.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
            await using var f = File.Open(item.ImagePath, FileMode.Create);
            await stream.CopyToAsync(f, token);
        }

        return data.Results.Values.OrderByDescending(x => x.CreatedAt).ToList();
    }

    private CategoryPage DeserializeData(Stream content)
    {
        try
        {
            return JsonSerializer.Deserialize<CategoryPage>(content, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy()
            });
        }
        catch (JsonException e) when (e.Message.Contains("no files in cat"))
        {
            return new CategoryPage() { Results = new Dictionary<string, Mod>() };
        }
    }

    public async Task DownloadMod(Filesystem filesystem, IMod mod, CancellationToken token)
    {
        render.Append($"> Downloading mod: {mod.Name} ({(double)mod.Size/1024/1024:F2} MiB)");
        var modDir = filesystem.ModDir(mod);
        if (modDir.Exists)
        {
            // clear directory except for original downloaded file
            foreach (var file in modDir.EnumerateFiles().Where(x => !x.Name.StartsWith(".mod")))
            {
                file.Delete();
            }

            foreach (var dir in modDir.EnumerateDirectories())
            {
                dir.Delete(true);
            }
        }

        modDir.Create();
        var response = await client.GetAsync(mod.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        var originalName = response.Content.Headers.ContentDisposition.FileName ?? response.Content.Headers.ContentDisposition.FileNameStar;
        var filteredName = originalName.Trim().Trim('"');
        var extension = Path.GetExtension(filteredName);
        var fileName = $".mod{extension}";
        var contentSize = response.Content.Headers.ContentLength;
        var downloadedFile = new FileInfo(Path.Combine(modDir.FullName, fileName));
        if (!downloadedFile.Exists || downloadedFile.Length != contentSize)
        {
            if (downloadedFile.Exists)
            {
                // check is required because IOException otherwise
                downloadedFile.Delete();
            }

            await using var dstStream = downloadedFile.Create();
            await using var s = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
            await CopyStreamWithProgress(s, dstStream, contentSize ?? 0, token);
        }

        try
        {
            await using var f = downloadedFile.OpenRead();
            var reader = ReaderFactory.Open(f);
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                render.Append($"> Extracting {reader.Entry.Key}...");
                reader.WriteEntryToDirectory(modDir.FullName, new ExtractionOptions() {ExtractFullPath = false, Overwrite = false});
            }
        }
        catch (InvalidOperationException e)
        {
            // SharpCompress doesnt support streaming 7zip, for instance
            var archive = ArchiveFactory.Open(downloadedFile.FullName);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                render.Append($"> Extracting {entry.Key}...");
                entry.WriteToDirectory(modDir.FullName, new ExtractionOptions() {ExtractFullPath = true, Overwrite = true});
            }
        }
    }

    public async Task<IHtmlDocument> GetNewsWiki(CancellationToken token)
    {
        var response = await client.GetAsync(Constants.WikiPage, token);
        await using var contentStream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        return await parser.ParseDocumentAsync(contentStream, token);

    }

    public async Task<long> GetLatestCommunityPatchId(CancellationToken token)
    {
        var builder = new UriBuilder(Constants.FindMapUrl);
        builder.Query = "rflName=latestrfgcommpack";
        var url = builder.Uri;

        var response = await client.GetAsync(url, token);
        var content = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(token);
        var parts = content.Trim('\0').Split().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var id = long.Parse(parts.Last());
        return id;
    }

    private static string BbCodeToMarkdown(string input)
    {
        // BUG: markdown renderer breaks when there is a link inside bold/italic/..
        try
        {
            input = Regex.Unescape(input);
        }
        catch (RegexParseException)
        {
            // BUG: some texts F up regex un-escaping, ignore it    
        }
        input = input.Replace("\r", "\n");
        input = bold.Replace(input, "**");
        input = italic.Replace(input, "*");
        input = underline.Replace(input, "__");
        input = strike.Replace(input, "~~");

        var match = url.Match(input);
        while (match.Success)
        {

            var text = match.Groups[2].Value;
            var link = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(link))
            {
                link = text;
            }

            input = input[..match.Index] + $"[{text}]({link})" + input[(match.Index + match.Length)..];
            match = url.Match(input);
        }

        return input;
    }

    public async Task CopyStreamWithProgress(Stream source, Stream destination, long expectedSize, CancellationToken cancellationToken)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!source.CanRead)
            throw new ArgumentException("Has to be readable", nameof(source));
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (!destination.CanWrite)
            throw new ArgumentException("Has to be writable", nameof(destination));

        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;
        var totalMb = (double) expectedSize / 1024 / 1024;
        long lastReported = 0;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            var readMiB = (long)Math.Floor((double) totalBytesRead / 1024 / 1024);
            if (readMiB > 0 && readMiB % 10 == 0)
            {
                var current = readMiB / 10;
                if (current <= lastReported)
                {
                    continue;
                }
                lastReported = current;
                render.Append($"> {readMiB:F0} / {totalMb:F0} MiB");
            }
        }
    }

    private static Regex bold = new(@"\[/?b\]");
    private static Regex italic = new(@"\[/?i\]");
    private static Regex underline = new(@"\[/?u\]");
    private static Regex strike = new(@"\[/?s\]");
    private static Regex url = new(@"\[url=?(.*?)\](.*?)\[/url\]");
}
