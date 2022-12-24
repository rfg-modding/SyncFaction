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
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Services.FactionFiles;

public interface IStateProvider
{
    public State State { get; }
    public bool Initialized { get; }
}

public class FfClient
{
    private readonly HttpClient client;
    private readonly IStateProvider stateProvider;
    private readonly IFileSystem fileSystem;
    private readonly ILogger log;

    public FfClient(HttpClient client, IStateProvider stateProvider, IFileSystem fileSystem, ILogger<FfClient> log)
    {
        this.client = client;
        this.stateProvider = stateProvider;
        this.fileSystem = fileSystem;
        this.log = log;
    }

    public async Task<IReadOnlyList<IMod>> GetMods(Category category, IGameStorage storage, CancellationToken token)
    {
        if (stateProvider.State.MockMode is true && category is Category.ModsStandalone)
        {
            // use fake mod info for testing until community patch is uploaded to FF
            return new List<Mod>
            {
                new()
                {
                    Id = 666,
                    Size = 108996431,
                    DownloadUrl = "https://www.factionfiles.com/ffdownload.php?id=2730"
                }
            };
        }

        if (category is Category.Local)
        {
            // this does not belong here really, but is very convenient to parallelize calls
            return GetLocalMods(storage);
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
            throw new InvalidOperationException("FactionFiles API returned partial data, app update required to support this!");
        }

        foreach (var item in data.Results.Values)
        {
            item.Category = category;
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

            item.Status = GetModStatus(item, storage);
        }

        return data.Results.Values.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public ModStatus GetModStatus(Mod item, IGameStorage storage)
    {
        var modDir = storage.GetModDir(item);
        var incompleteDataFile = fileSystem.FileInfo.FromFileName(Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        if (modDir.Exists && !incompleteDataFile.Exists)
        {
            return ModStatus.Ready;
        }

        return ModStatus.None;
    }

    private List<LocalMod> GetLocalMods(IAppStorage storage)
    {
        List<LocalMod> mods = new ();
        foreach (var dir in storage.App.EnumerateDirectories())
        {
            if (dir.Name.StartsWith("."))
            {
                // skip unix-hidden files
                continue;
            }

            if (dir.Name.StartsWith("Mod_") && dir.Name.Substring(4).All(char.IsDigit))
            {
                // skip downloaded mods
                continue;
            }

            var mod = new LocalMod()
            {
                Name = dir.Name,
                Size = 0,
                DownloadUrl = null,
                ImageUrl = null,
                Status = ModStatus.Ready
            };
            mods.Add(mod);
        }

        return mods;
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
        log.LogDebug($"Installing mod: {mod.Name} ({(double)mod.Size/1024/1024:F2} MiB)");
        var incompleteDataFile = fileSystem.FileInfo.FromFileName(Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        if (modDir.Exists && !incompleteDataFile.Exists)
        {
            // if everything was successfully downloaded and extracted before, dont touch files: this allows user to fiddle with mod contents
            log.LogInformation("Found existing data, skip downloading and extraction");
            mod.Status = ModStatus.Ready;
            return true;
        }

        modDir.Create();
        incompleteDataFile.Create();
        incompleteDataFile.Refresh();


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
            mod.Status = ModStatus.Failed;
            return false;
        }

        var dstFile = fileSystem.FileInfo.FromFileName(Path.Combine(modDir.FullName, fileName));

        var downloadResult = await DownloadWithResume(dstFile, contentSize.Value, mod.DownloadUrl, token);
        if (downloadResult == false)
        {
            mod.Status = ModStatus.Failed;
            return false;
        }

        var extractResult = await ExtractArchive(dstFile, modDir, incompleteDataFile, token);
        if (extractResult == false)
        {
            mod.Status = ModStatus.Failed;
            return false;
        }

        incompleteDataFile.Delete();
        mod.Status = ModStatus.Ready;
        return true;
    }

    public async Task<bool> DownloadWithResume(IFileInfo dstFile, long contentLength, string modDownloadUrl, CancellationToken token)
    {
        log.LogInformation("Downloading `{url}`", modDownloadUrl);
        if (stateProvider.State.MockMode is true && dstFile.Exists)
        {
            // allow replacing with any other file
            return true;
        }

        if (dstFile.Exists && dstFile.Length == contentLength)
        {
            // skip only if fully downloaded before
            log.LogInformation("Found existing data, skip downloading");
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

    private async Task<bool> ExtractArchive(IFileInfo downloadedFile, IDirectoryInfo modDir, IFileInfo incompleteDataFile, CancellationToken token)
    {
        log.LogInformation("Extracting `{file}`", downloadedFile.FullName);
        // if everything was successfully extracted before, dont touch anything: this allows user to fiddle with files
        incompleteDataFile.Refresh();
        if (!incompleteDataFile.Exists)
        {
            log.LogInformation("Found existing data, skip extraction");
            return true;
        }

        var options = new ExtractionOptions {ExtractFullPath = true, Overwrite = true};
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
            log.LogWarning("This is a `.7z` archive. Falling back to slow extraction method. Sorry!");
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
        if (stateProvider.State.MockMode is true)
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
                log.LogInformation($"+ {readMiB:F0} / {totalMb:F0} MiB");
            }
        }
    }

    private static Regex bold = new(@"\[/?b\]");
    private static Regex italic = new(@"\[/?i\]");
    private static Regex underline = new(@"\[/?u\]");
    private static Regex strike = new(@"\[/?s\]");
    private static Regex url = new(@"\[url=?(.*?)\](.*?)\[/url\]");
}
