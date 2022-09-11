using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Moq;
using SyncFaction.Core.Services;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFactionTests;

public class Tests
{
    private string fileName;
    private byte[] fourBytes;
    private FileStream stream;

    [SetUp]
    public void Setup()
    {
        fileName = "test.txt";
        fourBytes = new byte[] {4, 8, 15, 16};

    }

    [Test]
    public void Delete_FileDoesntExist_Works()
    {
        var action = () => File.Delete(fileName);
        action.Should().NotThrow();
        action.Should().NotThrow();
    }

    [Test]
    public void FileStreamManipulation_Works()
    {
        File.Delete(fileName);
        stream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write);
        stream.Write(fourBytes);
        stream.Dispose();
        new FileInfo(fileName).Length.Should().Be(4);

        stream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write);
        stream.Length.Should().Be(4);
        stream.CanSeek.Should().BeTrue();
        stream.Position.Should().Be(0);

        stream.Seek(0, SeekOrigin.End);
        stream.Position.Should().Be(4);

        stream.Seek(0, SeekOrigin.Begin);

        stream.Position.Should().Be(0);
        var buffer = new Span<byte>(new byte[4]);
        //var result = stream.Read(buffer);
        //result.Should().Be(4);
        //buffer.ToArray().Should().BeEquivalentTo(fourBytes);

        stream.Seek(0, SeekOrigin.End);
        stream.Position.Should().Be(4);
        stream.Write(fourBytes);
        stream.Flush();
        new FileInfo(fileName).Length.Should().Be(8);
        stream.Dispose();

        using var stream2 = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Read);
        stream2.Seek(0, SeekOrigin.Begin);
        stream2.Position.Should().Be(0);
        var buffer2 = new Span<byte>(new byte[8]);
        var result2 = stream2.Read(buffer2);
        result2.Should().Be(8);
        buffer2.ToArray().Should().BeEquivalentTo(fourBytes.Concat(fourBytes));
        stream2.Dispose();
    }

    [Test()]
    [Explicit("does not work in mock mode, disabled for development")]
    public async Task Test()
    {
        ILogger<FfClient> log = new NullLogger<FfClient>();
        var httpClient = new HttpClient();
        var fs = new FileSystem();
        var client = new FfClient(new StateProvider(), httpClient, fs, log);
        var dstFile = fs.FileInfo.FromFileName("out.bin");
        dstFile.Delete();
        var url = "https://www.factionfiles.com/ffdownload.php?id=5843";

        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        var contentSize = response.Content.Headers.ContentLength.Value;

        var result = await client.DownloadWithResume(dstFile, contentSize, url, CancellationToken.None);

        dstFile.Length.Should().Be(contentSize);
        result.Should().BeTrue();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            //stream.Dispose();
        }
        catch (ObjectDisposedException)
        {

        }

        File.Delete(fileName);
    }
}
