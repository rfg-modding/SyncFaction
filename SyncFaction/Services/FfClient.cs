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
using SharpCompress.Common;
using SharpCompress.Readers;
using SyncFaction.Services.FactionFiles;

namespace SyncFaction.Services;

public class FfClient
{
    private readonly HttpClient client;

    public FfClient(HttpClient client)
    {
        this.client = client;
    }

    public async Task<List<Item>> GetMods(Category category, CancellationToken token)
    {
        // NOTE: pagination currently is not implemented, everything is returned on first page
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
            return new CategoryPage() { Results = new Dictionary<string, Item>() };
        }
    }

    public async Task<DirectoryInfo> DownloadMod(DirectoryInfo baseDir, IMod mod, CancellationToken token)
    {
        var modDir = new DirectoryInfo(Path.Combine(baseDir.FullName, mod.IdString));
        if (modDir.Exists)
        {
            modDir.Delete(true);
        }

        modDir.Create();
        var response = await client.GetAsync(mod.DownloadUrl, token);
        await using var s = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);

        /*
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? "file.zip";
        var downloadedFile = new FileInfo(Path.Combine(modDir.FullName, fileName));
        await using var dstStream = downloadedFile.OpenWrite();
        await s.CopyToAsync(dstStream, token);
        await dstStream.DisposeAsync();
        */

        var reader = ReaderFactory.Open(s);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            Console.WriteLine(reader.Entry.Key);
            reader.WriteEntryToDirectory(modDir.FullName,
                new ExtractionOptions() { ExtractFullPath = false, Overwrite = false });
        }

        return modDir;
    }

    public async Task<IHtmlDocument> GetNewsWiki(CancellationToken token)
    {
        var response = await client.GetAsync(Constants.WikiPage, token);
        await using var contentStream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        return await parser.ParseDocumentAsync(contentStream, token);

    }

    private static string BbCodeToMarkdown(string input)
    {
        // BUG: markdown renderer breaks when there is a link inside bold/italic/..
        input = Regex.Unescape(input);
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

    private static Regex bold = new(@"\[/?b\]");
    private static Regex italic = new(@"\[/?i\]");
    private static Regex underline = new(@"\[/?u\]");
    private static Regex strike = new(@"\[/?s\]");
    private static Regex url = new(@"\[url=?(.*?)\](.*?)\[/url\]");
}
