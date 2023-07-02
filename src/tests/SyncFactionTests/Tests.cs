using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using FluentAssertions;
using MathNet.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Core.Services.FactionFiles;
using SyncFaction.ModManager.XmlModels;

namespace SyncFactionTests;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Tests")]
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tests")]
public class Tests
{
    private string fileName;
    private byte[] fourBytes;
    private FileStream stream;

    [SetUp]
    public void Setup()
    {
        fileName = "test.txt";
        fourBytes = new byte[]
        {
            4,
            8,
            15,
            16
        };
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

        System.IO.File.Delete(fileName);
    }

    [Test]
    public void Delete_FileDoesntExist_Works()
    {
        var action = () => System.IO.File.Delete(fileName);
        action.Should().NotThrow();
        action.Should().NotThrow();
    }

    [Test]
    public void FileStreamManipulation_Works()
    {
        System.IO.File.Delete(fileName);
        stream = System.IO.File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write);
        stream.Write(fourBytes);
        stream.Dispose();
        new FileInfo(fileName).Length.Should().Be(4);

        stream = System.IO.File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write);
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

        using var stream2 = System.IO.File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Read);
        stream2.Seek(0, SeekOrigin.Begin);
        stream2.Position.Should().Be(0);
        var buffer2 = new Span<byte>(new byte[8]);
        var result2 = stream2.Read(buffer2);
        result2.Should().Be(8);
        buffer2.ToArray().Should().BeEquivalentTo(fourBytes.Concat(fourBytes));
        stream2.Dispose();
    }

    [Test]
    [Explicit("does not work in mock mode, disabled for development")]
    public async Task Test()
    {
        ILogger<FfClient> log = new NullLogger<FfClient>();
        var httpClient = new HttpClient();
        var fs = new FileSystem();
        var client = new FfClient(httpClient, Mock.Of<IStateProvider>(), fs, log);
        var dstFile = fs.FileInfo.New("out.bin");
        dstFile.Delete();
        var url = "https://www.factionfiles.com/ffdownload.php?id=5843";
        var mod = new Mod { DownloadUrl = url };

        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        var contentSize = response.Content.Headers.ContentLength.Value;

        var result = await client.DownloadWithResume(dstFile, contentSize, mod, CancellationToken.None);

        dstFile.Length.Should().Be(contentSize);
        result.Should().BeTrue();
    }

    [Test]
    [Explicit("Just for testing")]
    public void TestXml()
    {
        var doc = new XmlDocument();
        doc.Load(@"C:\vault\rfg\downloaded_mods\filtered\Vehicle Camera Options v1.01\modinfo.xml");
        doc.DocumentElement.Name.Should().Be("Mod");
        var changes = doc.DocumentElement["Changes"];
        var name = changes.SelectSingleNode("/Edit/Tweak_Table_Entry/Name");
        name.Should().NotBeNull();
    }

    [Test]
    [Explicit("Just for testing")]
    public void TestXmlDeserializeNode()
    {
        var doc = new XmlDocument();
        doc.Load(@"C:\vault\rfg\test.xml");
        var node = doc.DocumentElement;
        var dummy = new List<XmlNode> { node };
        var wrapper = dummy.Wrap();
        var overrides = new XmlAttributeOverrides();
        var attrs = new XmlAttributes
        {
            XmlRoot = new XmlRootAttribute("syncfaction_holder"),
            //XmlArray = new XmlArrayAttribute("syncfaction_holder"),
            //XmlArrayItems = {new XmlArrayItemAttribute(typeof(Replace)), new XmlArrayItemAttribute(typeof(Edit))}
            XmlElements =
            {
                new XmlElementAttribute(nameof(Replace), typeof(Replace)),
                new XmlElementAttribute(nameof(Edit), typeof(Edit))
            }
        };
        overrides.Add(typeof(List<IChange>), attrs);
        var serializer = new XmlSerializer(typeof(TypedChangesHolder));
        var reader = new XmlNodeReader(wrapper);
        var tc = (TypedChangesHolder) serializer.Deserialize(reader);
        tc.Should().NotBeNull();
        tc.TypedChanges.Should().NotBeNull().And.NotHaveCount(0);
    }

    [Test]
    [Explicit("Just for testing")]
    public void TestRegression()
    {
        var xdata = new double[]
        {
            10,
            20,
            30
        };
        var ydata = new double[]
        {
            15,
            20,
            25
        };

        var p = Fit.Line(xdata, ydata);

        p.A.Should().Be(10);
        p.B.Should().Be(0.5);
    }

    [Test]
    [Explicit("Just for testing")]
    public void TestRegressionFunc()
    {
        var xdata = new double[]
        {
            10,
            20,
            30
        };
        var ydata = new double[]
        {
            15,
            20,
            25
        };

        var f = Fit.LineFunc(xdata, ydata);

        // a + bx
        f(1).Should().Be(10.5);
        f(2).Should().Be(11);
        f(10).Should().Be(15);
    }

    [Test]
    [Explicit("Just for testing")]
    public void TestRegressionFuncTime()
    {
        var xItems = new double[]
        {
            0,
            113,
            137,
            172,
            193,
            205
        };
        var yTimes = new double[]
        {
            0,
            5,
            10,
            15,
            25,
            30
        };

        var f = Fit.LineFunc(xItems, yTimes);

        // a + bx
        //f(100).Should().Be(10);
        //f(150).Should().Be(16);
        //f(200).Should().Be(22);
        f(300).Should().Be(36);
    }
    //[5.2090918,10.3149094,15.6657531,24.9925824,30.3302552],"Measures":[113,137,172,193,205]}}

    [Test]
    [Explicit("Just for testing")]
    public void TestEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "авб где жзи клм ноп рст уфх цчш щъы ьэю я".ToUpperInvariant();
        var a = Encoding.GetEncoding("windows-1251").GetBytes(text);
        var b = Encoding.GetEncoding("windows-1252").GetString(a);
        Console.WriteLine(text);
        Console.WriteLine(b);
        Assert.Pass();
    }
}
