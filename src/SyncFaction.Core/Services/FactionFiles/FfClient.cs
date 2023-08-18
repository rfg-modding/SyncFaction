using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using JorgeSerrano.Json;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SyncFaction.Core.Models;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Services.Files;
using SyncFaction.Extras;

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

        this.client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Title.AppName, Title.Version.Replace(':', '-').Replace(' ', '_')));
    }

    public async Task<IReadOnlyList<IMod>> GetFfMods(Category category, IGameStorage storage, CancellationToken token)
    {
        // NOTE: pagination currently is not implemented on FF side, everything is returned on first page
        log.LogInformation("Reading FactionFiles category: {category}", category);
        var builder = new UriBuilder(Constants.ApiUrl) { Query = $"cat={category:D}&page=1" };
        var url = builder.Uri;

        log.LogTrace("Request: GET {url}", url);
        var response = await client.GetAsync(url, token);
        await using var content = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);

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

            var imageFile = storage.FileSystem.Path.Combine(storage.Img.FullName, $"ff_{item.Id}.png");
            if (fileSystem.File.Exists(imageFile))
            {
                log.LogTrace("Image exists [{file}]", imageFile);
                item.ImagePath = imageFile;
                continue;
            }

            log.LogTrace("Request: GET {url}", url);
            var image = await client.GetAsync(item.ImageThumb4By3Url, token);
            log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);
            await using var stream = await image.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
            log.LogTrace("Writing image {file}", imageFile);
            await using var f = fileSystem.File.Open(imageFile, FileMode.Create);
            await stream.CopyToAsync(f, token);
            item.ImagePath = imageFile;
        }

        return data.Results.Values.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<bool> DownloadAndUnpackMod(IDirectoryInfo modDir, IMod mod, CancellationToken token)
    {
        log.LogTrace("Downloading [{mod}] ({size:F2} MiB)", mod.Name, (double) mod.Size / 1024 / 1024);
        var incompleteDataFile = fileSystem.FileInfo.New(fileSystem.Path.Join(modDir.FullName, Constants.IncompleteDataFile));
        if (modDir.Exists && !incompleteDataFile.Exists)
        {
            // if everything was successfully downloaded and extracted before, dont touch files: this allows user to fiddle with mod contents
            mod.Status = OnlineModStatus.Ready;
            log.LogInformation("Found existing data for `{id}`, skip downloading and extraction", mod.Id);
            return true;
        }

        modDir.Create();
        incompleteDataFile.Create().Close();
        incompleteDataFile.Refresh();
        log.LogTrace("Created incomplete data file [{file}]", incompleteDataFile.FullName);

        var remoteFileInfo = await GetRemoteFileInfo(mod, token);
        if (remoteFileInfo is null)
        {
            log.LogTrace("Failed to get remote file info");
            return false;
        }

        var dstFile = fileSystem.FileInfo.New(modDir.FileSystem.Path.Combine(modDir.FullName, remoteFileInfo.FileName));
        var downloadResult = await DownloadWithResume(dstFile, remoteFileInfo.Size, mod, token);
        if (downloadResult == false)
        {
            mod.Status = OnlineModStatus.Failed;
            log.LogTrace("Download failed");
            return false;
        }

        var extractResult = await ExtractArchive(mod, dstFile, modDir, incompleteDataFile, token);
        if (extractResult == false)
        {
            mod.Status = OnlineModStatus.Failed;
            log.LogTrace("Extraction failed");
            return false;
        }

        await PersistDescription(modDir, mod);
        incompleteDataFile.Delete();
        log.LogTrace("Success! Deleted incomplete data file [{file}]", incompleteDataFile.FullName);
        mod.Status = OnlineModStatus.Ready;
        return true;
    }

    public async Task<IHtmlDocument> GetNewsWiki(CancellationToken token)
    {
        log.LogTrace("Request: GET {url}", Constants.WikiPage);
        var response = await client.GetAsync(Constants.WikiPage, token);
        log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);
        await using var contentStream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        var parser = new HtmlParser();
        return await parser.ParseDocumentAsync(contentStream, token);
    }

    public async Task<List<long>> ListPatchIds(string prefix, CancellationToken token)
    {
        var result = new List<long>();
        var i = 1;
        long? id;
        do
        {
            id = await GetIdBySearchString($"{prefix}{i}", token);
            if (id != null)
            {
                result.Add(id.Value);
                log.LogTrace("Found [{prefix}] patch, part [{i}], id [{id}]", prefix, i, id.Value);
            }

            i++;
        }
        while (id != null);

        return result;
    }

    private async Task CopyStreamWithProgress(IMod mod, Stream source, Stream destination, long expectedSize, CancellationToken cancellationToken)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (!source.CanRead)
        {
            throw new ArgumentException("Has to be readable", nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Has to be writable", nameof(destination));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            log.LogTrace("Copying stream for {id}: src {src}, dstPos {dstPos}, expectedSize {expectedSize}", mod.Id, source, destination.Position, expectedSize);
            var totalBytesRead = destination.Position;
            int bytesRead;
            var totalMiB = (double) expectedSize / 1024 / 1024;
            long lastReported = 0;
            while ((bytesRead = await ReadWithTimeout(source, buffer, cancellationToken)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                var readMiB = (long) Math.Floor((double) totalBytesRead / 1024 / 1024);
                if (readMiB > 0 && readMiB % 10 == 0)
                {
                    var current = readMiB / 10;
                    if (current <= lastReported)
                    {
                        continue;
                    }

                    lastReported = current;
                    log.LogInformation(Md.Bullet.Id(), "Downloading {id}: {read:F0} / {total:F0} MiB", mod.Id, readMiB, totalMiB);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Each individual stream read from HttpClient Response may hang infinitely on network loss. We have to time-limit every read
    /// </summary>
    private static async ValueTask<int> ReadWithTimeout(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            return await stream.ReadAsync(buffer, cts.Token);
        }
        catch (Exception e) when (cts.IsCancellationRequested)
        {
            throw new IOException("Timed out while reading from network", e);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended")]
    internal async Task<bool> DownloadWithResume(IFileInfo dstFile, long contentLength, IMod mod, CancellationToken token)
    {
        log.LogTrace("Downloading [{id}] to {file}", mod.Id, dstFile.FullName);
        if (dstFile.Exists && dstFile.Length == contentLength)
        {
            // skip only if fully downloaded before
            log.LogInformation("Found existing data for `{id}`, skip downloading", mod.Id);
            return true;
        }

        await using (var dstStream = dstFile.Open(FileMode.OpenOrCreate, FileAccess.Write))
        {
            var hasProgress = true;
            dstStream.Seek(0, SeekOrigin.End);
            do
            {
                var positionBefore = dstStream.Position;
                try
                {
                    await using var srcStream = await GetHttpStream(mod, contentLength, dstStream.Position, token);
                    await CopyStreamWithProgress(mod, srcStream, dstStream, contentLength, token);
                }
                catch (Exception e)
                {
                    log.LogTrace(e, "Download exception!");
                    if (token.IsCancellationRequested)
                    {
                        log.LogInformation("Download canceled: `{id}`", mod.Id);
                        return false;
                    }

                    log.LogInformation("Error while downloading [{id}], continue in 5 seconds... Details: `{message}`", mod.Id, e.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    continue;
                }

                hasProgress = positionBefore > dstStream.Position;
            }
            while (hasProgress && !token.IsCancellationRequested);
        }

        dstFile.Refresh();
        return dstFile.Length == contentLength;
    }

    private CategoryPage? DeserializeData(Stream content)
    {
        try
        {
            return JsonSerializer.Deserialize<CategoryPage>(content, new JsonSerializerOptions { PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy() });
        }
        catch (JsonException e) when (e.Message.Contains("no files in cat"))
        {
            return new CategoryPage { Results = new Dictionary<string, Mod>() };
        }
    }

    /// <summary>
    /// Figure out file size and extension: FF can host zip, rar, etc
    /// </summary>
    private async Task<RemoteFileInfo?> GetRemoteFileInfo(IMod mod, CancellationToken token)
    {
        if (mod.DownloadUrl.Contains(Constants.CdnUrl))
        {
            // mod retrieved from CDN already has all metadata
            var cdnFileName = fileSystem.Path.GetFileName(mod.DownloadUrl);
            var result = new RemoteFileInfo(cdnFileName, mod.Size);
            log.LogTrace("Mod [{id}] info from CDN: {info}", mod.Id, result);
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, mod.DownloadUrl);
        log.LogTrace("Request: HEAD {url}", request.RequestUri);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var originalName = response.Content.Headers.ContentDisposition?.FileName ?? response.Content.Headers.ContentDisposition?.FileNameStar ?? string.Empty;
        log.LogTrace("Response: {code}, originalName [{originalName}]", response.StatusCode, originalName);
        var filteredName = originalName.Trim().Trim('"');
        var extension = fileSystem.Path.GetExtension(filteredName);
        var fileName = $".mod{extension}";
        var contentSize = response.Content.Headers.ContentLength;
        if (contentSize == null)
        {
            log.LogError("FF server did not return content size. Can not download mod `{id}`!", mod.Id);
            mod.Status = OnlineModStatus.Failed;
            return null;
        }

        var info = new RemoteFileInfo(fileName, contentSize.Value);
        log.LogTrace("Mod [{id}] info from FF: {info}", mod.Id, info);
        return info;
    }

    private async Task PersistDescription(IDirectoryInfo modDir, IMod mod)
    {
        // persist info for offline usage
        var descriptionFile = fileSystem.FileInfo.New(fileSystem.Path.Combine(modDir.FullName, Constants.ModDescriptionFile));
        if (descriptionFile.Exists)
        {
            log.LogTrace("Description file already exists: [{file}], length [{length}]", descriptionFile.FullName, descriptionFile.Length);
            return;
        }

        var json = JsonSerializer.Serialize(mod, new JsonSerializerOptions { WriteIndented = true });
        await using var writer = descriptionFile.CreateText();
        await writer.WriteAsync(json);
        log.LogTrace("Saved description file [{file}]", descriptionFile.FullName);
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
            : mod.Id.ToString(CultureInfo.InvariantCulture);
        var cdnUrl = $"{Constants.CdnUrl}/mirror/{id}";
        log.LogInformation("Trying CDN: {url}", cdnUrl);
        using var cdnRequest = new HttpRequestMessage(HttpMethod.Get, cdnUrl);
        cdnRequest.Headers.Range = new RangeHeaderValue(position, contentLength);
        using var responseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, responseTimeout.Token);
        try
        {
            log.LogTrace("Request: GET {url}, range: {range}", cdnUrl, cdnRequest.Headers.Range);
            var response = await client.SendAsync(cdnRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);
            return await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
        }
        catch (Exception e)
        {
            // it's ok, fall back to normal FF download
            log.LogTrace(e, "CDN mirror not available");
            return null;
        }
    }

    private async Task<Stream> GetFfHttpStream(IMod mod, long contentLength, long position, CancellationToken token)
    {
        log.LogInformation("Trying FactionFiles: {url}", mod.DownloadUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, mod.DownloadUrl);
        request.Headers.Range = new RangeHeaderValue(position, contentLength);
        log.LogTrace("Request: GET {url}, range: {range}", mod.DownloadUrl, request.Headers.Range);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        log.LogTrace("Response: {code}, length {contentLength}", response.StatusCode, response.Content.Headers.ContentLength);
        return await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(token);
    }

    private async Task<bool> ExtractArchive(IMod mod, IFileInfo downloadedFile, IDirectoryInfo modDir, IFileInfo incompleteDataFile, CancellationToken token)
    {
        // TODO get rid of top-level directory if it's the only thing on top level, eg fast_vehicles.zip/fast_vehicles/*
        log.LogInformation("Extracting {id}: `{file}`", mod.Id, downloadedFile.FullName);
        // if everything was successfully extracted before, dont touch anything: this allows user to fiddle with files
        incompleteDataFile.Refresh();
        if (!incompleteDataFile.Exists)
        {
            log.LogInformation("Found existing data for `{id}`, skip extraction", mod.Id);
            return true;
        }

        var options = new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        };
        try
        {
            await using var f = downloadedFile.OpenRead();
            using var reader = ReaderFactory.Open(f);
            while (reader.MoveToNextEntry())
            {
                token.ThrowIfCancellationRequested();
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                log.LogTrace("Extracting [{id}] archive entry: [{file}]", mod.Id, reader.Entry.Key);
                reader.WriteEntryToDirectory(modDir.FullName, options);
            }
        }
        catch (InvalidOperationException e)
        {
            log.LogTrace(e, "Streamed extraction failed for [{file}]", downloadedFile.FullName);
            // SharpCompress doesnt support streaming 7zip, fall back to slow method
            log.LogWarning("This is probably a **.7z** archive. Falling back to slow extraction method. Sorry!");
            using var archive = ArchiveFactory.Open(downloadedFile.FullName);
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                {
                    continue;
                }

                //log.LogTrace($"Extracting {entry.Key}...");
                log.LogTrace("Extracting [{id}] archive entry: [{file}]", mod.Id, entry.Key);
                entry.WriteToDirectory(modDir.FullName, options);
            }
        }

        return true;
    }

    private async Task<long?> GetIdBySearchString(string searchString, CancellationToken token)
    {
        // TODO remove this block. left for debugging with old patch/update naming scheme
        {
            if (searchString == "rfgterraform1")
            {
                searchString = "rfgcommunitypatch";
            }

            if (searchString.StartsWith("rfgterraform", StringComparison.Ordinal))
            {
                var number = int.Parse(searchString["rfgterraform".Length..], CultureInfo.InvariantCulture) - 1;
                searchString = $"rfgcommunityupdate{number}";
            }
        }

        var builder = new UriBuilder(Constants.FindMapUrl) { Query = $"rflName={searchString}" };
        var url = builder.Uri;
        log.LogTrace("Request: GET {url}", url);
        var response = await client.GetAsync(url, token);
        var content = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(token);
        log.LogTrace("Response: {code} {content}", response.StatusCode, content);
        var parts = content.Trim('\0').Split().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var lastPart = parts.Last();
        if (lastPart.Equals("notfound", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return long.Parse(parts.Last(), CultureInfo.InvariantCulture);
    }

    private static string BbCodeToMarkdown(string input)
    {
        // NOTE: markdown renderer breaks when there is a link inside bold/italic/..
        try
        {
            input = Regex.Unescape(input);
        }
        catch (RegexParseException)
        {
            // NOTE: some texts F up regex un-escaping, ignore it
        }

        input = input.Replace("\r", "\n");
        input = Bold.Replace(input, "**");
        input = Italic.Replace(input, "*");
        input = Underline.Replace(input, "__");
        input = Strike.Replace(input, "~~");

        var match = Url.Match(input);
        while (match.Success)
        {
            var text = match.Groups[2].Value;
            var link = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(link))
            {
                link = text;
            }

            input = input[..match.Index] + $"[{text}]({link})" + input[(match.Index + match.Length)..];
            match = Url.Match(input);
        }

        return input;
    }

    private static readonly Regex Bold = new(@"\[/?b\]");
    private static readonly Regex Italic = new(@"\[/?i\]");
    private static readonly Regex Underline = new(@"\[/?u\]");
    private static readonly Regex Strike = new(@"\[/?s\]");
    private static readonly Regex Url = new(@"\[url=?(.*?)\](.*?)\[/url\]");

    private record RemoteFileInfo(string FileName, long Size);
}
