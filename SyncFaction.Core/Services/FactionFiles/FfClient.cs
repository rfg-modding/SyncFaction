using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using FastHashes;
using JorgeSerrano.Json;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.Files;

namespace SyncFaction.Core.Services.FactionFiles;

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

        this.client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SyncFaction"));
    }

    public async Task<IReadOnlyList<IMod>> GetMods(Category category, IGameStorage storage, CancellationToken token)
    {
        if (category is Category.Local)
        {
            // this does not belong here really, but is very convenient to parallelize calls
            return GetLocalMods(storage);
        }

        if (category is Category.Dev)
        {
            // this does not belong here too
            return await GetDevMods(storage, token);
        }

        return await GetFfMods(category, storage, token);
    }

    private async Task<IReadOnlyList<IMod>> GetFfMods(Category category, IGameStorage storage, CancellationToken token)
    {
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
            item.ImagePath = Path.Combine(storage.Img.FullName, $"ff_{item.Id}.png");
            await using var f = File.Open(item.ImagePath, FileMode.Create);
            await stream.CopyToAsync(f, token);

            item.Status = GetModStatus(item, storage);
        }

        return data.Results.Values.OrderByDescending(x => x.CreatedAt).ToList();
    }

    private async Task<IReadOnlyList<IMod>> GetDevMods(IGameStorage storage, CancellationToken token)
    {
        // TODO: any pagination?
        log.LogDebug($"Reading CDN for dev mods");
        var url = new UriBuilder(Constants.CdnListUrl)
        {
            Query = $"AccessKey={Constants.CdnReadApiKey}"
        }.Uri;

        var response = await client.GetAsync(url, token);
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
            mod.Status = GetModStatus(mod, storage);
            result.Add(mod);
        }

        return result.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public OnlineModStatus GetModStatus(Mod item, IGameStorage storage)
    {
        var modDir = storage.GetModDir(item);
        var incompleteDataFile = fileSystem.FileInfo.FromFileName(Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        var descriptionFile = fileSystem.FileInfo.FromFileName(Path.Combine(modDir.FullName, Constants.ModDescriptionFile));
        if (modDir.Exists && !incompleteDataFile.Exists && descriptionFile.Exists)
        {
            return OnlineModStatus.Ready;
        }

        return OnlineModStatus.None;
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

            if (dir.Name.StartsWith("Mod_"))
            {
                // skip downloaded mods
                continue;
            }

            var id = BitConverter.ToInt64(new MurmurHash64().ComputeHash( Encoding.UTF8.GetBytes(dir.Name.ToLowerInvariant()) ));
            var mod = new LocalMod()
            {
                Id = id,
                Name = dir.Name,
                Size = 0,
                DownloadUrl = null,
                ImageUrl = null,
                Status = OnlineModStatus.Ready
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
        log.LogDebug($"Downloading mod: {mod.Name} ({(double)mod.Size/1024/1024:F2} MiB)");
        var incompleteDataFile = fileSystem.FileInfo.FromFileName(Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        if (modDir.Exists && !incompleteDataFile.Exists)
        {
            // if everything was successfully downloaded and extracted before, dont touch files: this allows user to fiddle with mod contents
            await PersistDescription(modDir, mod);  // compatibility with older versions: try to save description anyway
            log.LogInformation("Found existing data, skip downloading and extraction");
            mod.Status = OnlineModStatus.Ready;
            return true;
        }

        modDir.Create();
        incompleteDataFile.Create().Close();
        incompleteDataFile.Refresh();

        var remoteFileInfo = await GetRemoteFileInfo(mod, token);
        if (remoteFileInfo is null)
        {
            return false;
        }

        var dstFile = fileSystem.FileInfo.FromFileName(Path.Combine(modDir.FullName, remoteFileInfo.FileName));

        var downloadResult = await DownloadWithResume(dstFile, remoteFileInfo.Size, mod, token);
        if (downloadResult == false)
        {
            mod.Status = OnlineModStatus.Failed;
            return false;
        }

        var extractResult = await ExtractArchive(dstFile, modDir, incompleteDataFile, token);
        if (extractResult == false)
        {
            mod.Status = OnlineModStatus.Failed;
            return false;
        }

        await PersistDescription(modDir, mod);
        incompleteDataFile.Delete();
        mod.Status = OnlineModStatus.Ready;
        return true;
    }

    private record RemoteFileInfo(string FileName, long Size);

    private async Task<RemoteFileInfo?> GetRemoteFileInfo(IMod mod, CancellationToken token)
    {
        if (mod.DownloadUrl.Contains(Constants.CdnUrl))
        {
            // mod retrieved from CDN already has all metadata
            var cdnFileName = Path.GetFileName(mod.DownloadUrl);
            return new RemoteFileInfo(cdnFileName, mod.Size);
        }

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
            mod.Status = OnlineModStatus.Failed;
            return null;
        }

        return new RemoteFileInfo(fileName, contentSize.Value);
    }

    private async Task PersistDescription(IDirectoryInfo modDir, IMod mod)
    {
        // persist info for offline usage
        var descriptionFile = fileSystem.FileInfo.FromFileName(Path.Combine(modDir.FullName, Constants.ModDescriptionFile));
        if (descriptionFile.Exists)
        {
            return;
        }

        var json = JsonSerializer.Serialize(mod, new JsonSerializerOptions() {WriteIndented = true});
        await using var writer = descriptionFile.CreateText();
        await writer.WriteAsync(json);
    }

    internal async Task<bool> DownloadWithResume(IFileInfo dstFile, long contentLength, IMod mod, CancellationToken token)
    {
        log.LogInformation("Downloading **{id}**: {name}", mod.Id, mod.Name);
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
                    await using var srcStream = await GetHttpStream(mod, contentLength, dstStream.Position, token);
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

    /// <summary>
    /// Try CDN if enabled. If disabled or failed, go to FF
    /// </summary>
    private async Task<Stream> GetHttpStream(IMod mod, long contentLength, long position, CancellationToken token)
    {
        var s = stateProvider.State.UseCdn is true
            ? await GetCdnStream(mod, contentLength, position, token)
            : null;
        return s ?? await GetFfHttpStream(mod, contentLength, position, token);
    }

    private async Task<Stream?> GetCdnStream(IMod mod, long contentLength, long position, CancellationToken token)
    {
        var id = mod.Category is Category.Dev
            ? mod.Name
            : mod.Id.ToString();
        var cdnUrl = $"{Constants.CdnUrl}/mirror/{id}";
        log.LogDebug("Trying CDN: {url}", cdnUrl);
        var cdnRequest = new HttpRequestMessage(HttpMethod.Get, cdnUrl);
        cdnRequest.Headers.Range = new RangeHeaderValue(position, contentLength);
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        try
        {
            var response = await client.SendAsync(cdnRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        }
        catch (Exception e)
        {
            // it's ok, fall back to normal FF download
            log.LogDebug(e, "CDN mirror not available");
            return null;
        }
    }

    private async Task<Stream> GetFfHttpStream(IMod mod, long contentLength, long position, CancellationToken token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, mod.DownloadUrl);
        request.Headers.Range = new RangeHeaderValue(position, contentLength);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        return await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
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
        var id = await GetIdBySearchString("rfgcommunitypatch", token);
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
