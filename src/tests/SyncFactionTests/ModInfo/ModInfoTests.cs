using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace SyncFactionTests.ModInfo;

public class ModInfoTests
{
    [Explicit("Reads all modinfo.xml files inside <game>/mods into objects and prints corresponding json")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllModInfos))]
    public void ReadAll(FileInfo fileInfo)
    {
        using var fileStream = fileInfo.OpenRead();
        var fs = new FileSystem();
        var dir = new DirectoryInfoWrapper(fs, fileInfo.Directory);
        var modInfo = SyncFaction.ModManager.XmlModels.ModInfo.LoadFromXml(fileStream, dir, Mock.Of<ILogger<SyncFaction.ModManager.XmlModels.ModInfo>>());
        if (modInfo is null)
        {
            Assert.Fail("should not be null!");
        }

        TestUtils.PrintJson(modInfo);
        fileStream.Position = 0;
        using var sr = new StreamReader(fileStream);
        var xml = sr.ReadToEnd();
        Console.WriteLine(xml);

        Assert.Pass();
    }

    [Explicit("Reads all modinfo.xml files inside <game>/mods and applies USER_INPUTs with [0] selection")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllModInfos))]
    public void ApplyEditsAll(FileInfo fileInfo)
    {
        using var fileStream = fileInfo.OpenRead();
        var fs = new FileSystem();
        var dir = new DirectoryInfoWrapper(fs, fileInfo.Directory);
        var modInfo = SyncFaction.ModManager.XmlModels.ModInfo.LoadFromXml(fileStream, dir, new NullLogger<SyncFaction.ModManager.XmlModels.ModInfo>());
        modInfo.ApplyUserInput();

        TestUtils.PrintJson(modInfo);
        fileStream.Position = 0;
        using var sr = new StreamReader(fileStream);
        var xml = sr.ReadToEnd();
        Console.WriteLine(xml);

        Assert.Pass();
    }
}
