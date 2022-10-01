using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using JorgeSerrano.Json;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SyncFaction.Core.Services.FactionFiles;

public class FfClient
{
    private readonly StateProvider stateProvider;
    private readonly HttpClient client;
    private readonly IFileSystem fileSystem;
    private readonly ILogger log;

    public FfClient(StateProvider stateProvider, HttpClient client, IFileSystem fileSystem, ILogger<FfClient> log)
    {
        this.stateProvider = stateProvider;
        this.client = client;
        this.fileSystem = fileSystem;
        this.log = log;
    }

    public async Task<List<Mod>> GetMods(Category category, CancellationToken token)
    {
        if (stateProvider.State.MockMode && category is Category.ModsStandalone)
        {
            // use fake mod info for testing until community patch is uploaded to FF
            return new List<Mod>()
            {
                new Mod()
                {
                    Id = 666,
                    Size = 108996431,
                    DownloadUrl = "https://www.factionfiles.com/ffdownload.php?id=2730"
                }
            };
        }
        // NOTE: pagination currently is not implemented, everything is returned on first page
        log.LogDebug($"Reading FactionFiles category: {category}");
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

    public async Task<bool> DownloadAndUnpackMod(IDirectoryInfo modDir, IMod mod, CancellationToken token)
    {
        log.LogDebug($"Downloading mod: {mod.Name} ({(double)mod.Size/1024/1024:F2} MiB)");
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
        var request = new HttpRequestMessage(HttpMethod.Head, mod.DownloadUrl);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        var originalName = response.Content.Headers.ContentDisposition?.FileName ?? response.Content.Headers.ContentDisposition?.FileNameStar ?? string.Empty;
        var filteredName = originalName.Trim().Trim('"');
        var extension = Path.GetExtension(filteredName);
        var fileName = $".mod{extension}";
        var contentSize = response.Content.Headers.ContentLength;
        if (contentSize == null)
        {
            log.LogInformation("FF server did not return content size. Can not download mod!");
            return false;
        }

        var dstFile = fileSystem.FileInfo.FromFileName(Path.Combine(modDir.FullName, fileName));

        var result = await DownloadWithResume(dstFile, contentSize.Value, mod.DownloadUrl, token);
        if (result == false)
        {
            return false;
        }

        return await ExtractArchive(dstFile, modDir, token);
    }

    public async Task<bool> DownloadWithResume(IFileInfo dstFile, long contentLength, string modDownloadUrl, CancellationToken token)
    {
        log.LogInformation("Downloading `{url}`", modDownloadUrl);
        if (stateProvider.State.MockMode && dstFile.Exists)
        {
            // allow replacing with any other file
            return true;
        }

        if (dstFile.Exists && dstFile.Length == contentLength)
        {
            return true;
        }

        await using (var dstStream = dstFile.Open(FileMode.OpenOrCreate, FileAccess.Write))
        {
            var hasProgress = true;
            dstStream.Seek(0, SeekOrigin.End);
            log.LogDebug($"Writing to `{dstFile.FullName}`");
            log.LogDebug($"Initial file position: `{dstStream.Position}`");
            log.LogDebug($"ContentLength: `{contentLength}`");
            do
            {
                var positionBefore = dstStream.Position;

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, modDownloadUrl);
                    request.Headers.Range = new RangeHeaderValue(dstStream.Position, contentLength);

                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    await using var srcStream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
                    await CopyStreamWithProgress(srcStream, dstStream, contentLength, token);
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }
                    log.LogInformation($"Error while downloading, continue in 5 seconds... Details: `{e.Message}`");
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    continue;
                }

                hasProgress = positionBefore > dstStream.Position;
            } while (hasProgress && !token.IsCancellationRequested);
        }

        dstFile.Refresh();
        return dstFile.Length == contentLength;
    }

    private async Task<bool> ExtractArchive(IFileInfo downloadedFile, IDirectoryInfo modDir, CancellationToken token)
    {
        log.LogInformation("Extracting `{url}`", downloadedFile.FullName);
        var options = new ExtractionOptions {ExtractFullPath = true, Overwrite = false};
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

                log.LogDebug($"Extracting {reader.Entry.Key}...");
                reader.WriteEntryToDirectory(modDir.FullName, options);
            }
        }
        catch (InvalidOperationException e)
        {
            // SharpCompress doesnt support streaming 7zip, fall back to slow method
            var archive = ArchiveFactory.Open(downloadedFile.FullName);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                log.LogDebug($"Extracting {entry.Key}...");
                entry.WriteToDirectory(modDir.FullName, options);
            }
        }

        return true;
    }

    public async Task<IHtmlDocument> GetNewsWiki(CancellationToken token)
    {
        var response = await client.GetAsync(Constants.WikiPage, token);
        await using var contentStream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        return await parser.ParseDocumentAsync(contentStream, token);

    }

    public async Task<long> GetCommunityPatchId(CancellationToken token)
    {
        long? id;
        if (stateProvider.State.MockMode)
        {
            // use fake mod id for testing until community patch is uploaded to FF
            id = 666;
        }
        else
        {
            id = await GetIdBySearchString("rfgcommunitypatch", token);
        }
        var humanReadableId = id == null ? "null" : id.ToString();
        log.LogInformation($"Community patch id: **{humanReadableId}**");
        return id ?? 0;
    }

    public async Task<List<long>> GetCommunityUpdateIds(CancellationToken token)
    {
        var result = new List<long>();
        int i = 1;
        long? id;
        do
        {
            id = await GetIdBySearchString($"rfgcommunityupdate{i}", token);
            if (id != null)
            {
                result.Add(id.Value);
                log.LogInformation($"Community update {i} id: **{id}**");
            }

            i++;
        } while (id != null);

        return result;
    }

    private async Task<long?> GetIdBySearchString(string searchString, CancellationToken token)
    {
        var builder = new UriBuilder(Constants.FindMapUrl);
        builder.Query = $"rflName={searchString}";
        var url = builder.Uri;
        var response = await client.GetAsync(url, token);
        var content = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(token);
        var parts = content.Trim('\0').Split().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var lastPart = parts.Last();
        if (lastPart.Equals("notfound", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return long.Parse(parts.Last());
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
        long totalBytesRead = destination.Position;
        int bytesRead;
        var totalMb = (double) expectedSize / 1024 / 1024;
        long lastReported = 0;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
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
                log.LogInformation($"* {readMiB:F0} / {totalMb:F0} MiB");
            }
        }
    }

    private static Regex bold = new(@"\[/?b\]");
    private static Regex italic = new(@"\[/?i\]");
    private static Regex underline = new(@"\[/?u\]");
    private static Regex strike = new(@"\[/?s\]");
    private static Regex url = new(@"\[url=?(.*?)\](.*?)\[/url\]");
}
