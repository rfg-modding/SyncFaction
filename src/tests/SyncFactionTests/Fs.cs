using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;

namespace SyncFactionTests;

public static class Fs
{
    public const int ModId = 22;

    public const string GameRoot    = "/game";
    public const string GameData    = "/game/data";
    public const string GameEtc     = "/game/etc";
    public const string GameDataEtc = "/game/data/etc";
    public const string GameDataSyncfaction = "/game/data/.syncfaction";

    public const string ModRoot    = "/game/data/.syncfaction/Mod_22";
    public const string ModData    = "/game/data/.syncfaction/Mod_22/data";
    public const string ModEtc     = "/game/data/.syncfaction/Mod_22/etc";
    public const string ModDataEtc = "/game/data/.syncfaction/Mod_22/data/etc";

    public const string VanillaRoot    = "/game/data/.syncfaction/.bak_vanilla";
    public const string VanillaData    = "/game/data/.syncfaction/.bak_vanilla/data";
    public const string VanillaEtc     = "/game/data/.syncfaction/.bak_vanilla/etc";
    public const string VanillaDataEtc = "/game/data/.syncfaction/.bak_vanilla/data/etc";

    public const string PatchRoot    = "/game/data/.syncfaction/.bak_patch";
    public const string PatchData    = "/game/data/.syncfaction/.bak_patch/data";
    public const string PatchEtc     = "/game/data/.syncfaction/.bak_patch/etc";
    public const string PatchDataEtc = "/game/data/.syncfaction/.bak_patch/data/etc";

    public const string ManagedRoot    = "/game/data/.syncfaction/.managed";
    public const string ManagedData    = "/game/data/.syncfaction/.managed/data";
    public const string ManagedEtc     = "/game/data/.syncfaction/.managed/etc";
    public const string ManagedDataEtc = "/game/data/.syncfaction/.managed/data/etc";

    public static MockFileSystem Init(Action<MockFileSystem>? action=null)
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(GameRoot);
        fs.AddDirectory(GameData);
        fs.AddDirectory(GameDataSyncfaction);
        action?.Invoke(fs);
        return fs;
    }

    public static MockFileSystem Clone(this MockFileSystem src, Action<MockFileSystem>? action=null)
    {
        var fs = new MockFileSystem();
        foreach (var path in src.AllFiles)
        {
            var data = new MockFileData(src.GetFile(path).TextContents);
            fs.AddFile(path, data);
        }
        action?.Invoke(fs);
        return fs;
    }

    public static class Hashes
    {
        public static IDictionary<string, string> Empty = MakeHashes(new string[]
        {
        });

        public static IDictionary<string, string> Exe = MakeHashes(new[]
        {
            "test.exe"
        });

        public static class Data
        {
            public static IDictionary<string, string> Vpp = MakeHashes(new[]
            {
                "data/archive.vpp_pc"
            });
        }

        public static IDictionary<string, string> MakeHashes(string[] fileNames) => new Dictionary<string, string>(fileNames.Select(InitHash));

        public static KeyValuePair<string, string> InitHash(string fileName) => new(fileName, $"hash of {fileName}");
    }

    public static void ShouldHaveSameFilesAs(this MockFileSystem actual, MockFileSystem expected)
    {
        // read all contents; remove "C:" and replace windows slashes to unix
        var actualDict = actual.AllFiles.ToDictionary(x => x.Substring(2).Replace('\\', '/'), x => actual.GetFile(x).TextContents);
        var expectedDict = expected.AllFiles.ToDictionary(x => x.Substring(2).Replace('\\', '/'), x => expected.GetFile(x).TextContents);
        actualDict.Should().Equal(expectedDict);
    }

    public static TestFile InitFile(this MockFileSystem fs)
    {
        return new TestFile(fs);
    }

    /// <summary>
    /// Combines several dictionaries. Throws on duplicate keys
    /// </summary>
    public static IDictionary<T1, T2> Combine<T1, T2>(IDictionary<T1, T2>[] sources)
        where T1 : notnull =>
        sources.SelectMany(dict => dict)
            .ToLookup(pair => pair.Key, pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.Single());
}

public class TestFile
{
    private readonly MockFileSystem fs;

    private MockFileData fileData = new MockFileData(string.Empty);

    private string name = string.Empty;

    public TestFile(MockFileSystem fs)
    {
        this.fs = fs;
    }

    public TestFile Data(string data)
    {
        fileData = new MockFileData(data);
        return this;
    }

    public TestFile Name(string name)
    {
        this.name = name;
        return this;
    }

    public TestFile In(string absPath)
    {
        var path = fs.Path.Combine(absPath, name);
        //fs.RemoveFile(path);  // TODO does it overwrite automatically?
        fs.AddFile(path, fileData);
        return this;
    }
}

/*
public static class Fs
{


        public static IDictionary<string, string> UnsupportedFiles = MakeFiles(new[]
        {

        });

        public static IDictionary<string, string> KnownFilesInRoot = MakeFiles(new[]
        {
            "/game/data/.syncfaction/Mod_22/test.exe",
            "/game/data/.syncfaction/Mod_22/test.vpp_pc",
        });
    }



    public static IDictionary<string, string> MakeFiles(string[] fileNames) => new Dictionary<string, string>(fileNames.Select(InitFile));

    public static IDictionary<string, string> MakeHashes(string[] fileNames) => new Dictionary<string, string>(fileNames.Select(InitHash));

    public static KeyValuePair<string, string> InitFile(string fileName) => new(fileName, new string($"content of {fileName}"));



    public static IDictionary<string, string> Copy(IDictionary<string, string> src, Action<IDictionary<string, string>> action)
    {
        var result = new Dictionary<string, string>(src);
        action(result);
        return result;
    }

    public static void Compare(MockFileSystem fs, IDictionary<string, string> expected)
    {
        // read all contents; remove "C:" and replace windows slashes to unix
        var allFiles = fs.AllFiles
            .Where(x => !x.EndsWith(".keep"))
            .ToDictionary(x => x.Substring(2).Replace('\\', '/'), x => fs.GetFile(x).TextContents);

        var expectedFiltered = expected
            .Where(x => !x.Key.EndsWith(".keep"))
            .ToDictionary(x => x.Key, x => x.Value);

        var serializedInfo = @$"FS:
{SerializeDictionary(allFiles)}

Expected:
{SerializeDictionary(expectedFiltered)}
";

        allFiles.Count.Should().Be(expectedFiltered.Count, serializedInfo);
        foreach (var kv in expectedFiltered)
        {
            var fileName = kv.Key;
            if (!allFiles.ContainsKey(fileName))
            {
                Assert.Fail($@"Result FS does not have expected file [{fileName}].
{serializedInfo}");
            }

            var content = allFiles[fileName];
            var expectedContent = expectedFiltered[fileName].TextContents;

            content.Should().Be(expectedContent);
        }
    }

    public static string SerializeDictionary(IEnumerable<KeyValuePair<string, string>> d)
    {
        var sb = new StringBuilder();
        foreach (var kv in d.OrderBy(x => x.Key))
        {
            //sb.AppendLine($"{kv.Key} => [{kv.Value}]");
            sb.AppendLine($"{kv.Key}");
        }

        return sb.ToString();
    }

    public static string SerializeDictionary(IEnumerable<KeyValuePair<string, string>> d)
    {
        return SerializeDictionary(d.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.TextContents)));
    }
}
*/
