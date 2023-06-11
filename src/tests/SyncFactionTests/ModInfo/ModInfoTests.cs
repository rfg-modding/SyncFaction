using Microsoft.Extensions.Logging;
using Moq;
using SyncFaction.ModManager;

namespace SyncFactionTests.ModInfo;

public class ModInfoTests
{
    [Explicit("Reads all modinfo.xml files inside <game>/mods into objects and prints corresponding json")]
    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllModInfos))]
    public void ReadAll(FileInfo fileInfo)
    {
        using var fileStream = fileInfo.OpenRead();
        var fs = new System.IO.Abstractions.FileSystem();
        var dir = new System.IO.Abstractions.DirectoryInfoWrapper(fs, fileInfo.Directory);
        var modInfo = new ModTools(Mock.Of<ILogger<ModTools>>()).LoadFromXml(fileStream, dir);
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
        var modTools = new ModTools(Mock.Of<ILogger<ModTools>>());
        var fs = new System.IO.Abstractions.FileSystem();
        var dir = new System.IO.Abstractions.DirectoryInfoWrapper(fs, fileInfo.Directory);
        var modInfo = modTools.LoadFromXml(fileStream, dir);
        modTools.ApplyUserInput(modInfo);

        TestUtils.PrintJson(modInfo);
        fileStream.Position = 0;
        using var sr = new StreamReader(fileStream);
        var xml = sr.ReadToEnd();
        Console.WriteLine(xml);

        Assert.Pass();
    }
}
