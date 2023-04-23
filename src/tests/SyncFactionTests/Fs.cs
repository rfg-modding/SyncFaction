using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using FluentAssertions;
using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class Fs
{
    public const int ModId1 = 1;
    public const int ModId2 = 2;
    public const int PchId1 = 66;
    public const int PchId2 = 77;

    public static class Game
    {
        public const string Root = "/game";
        public const string Data = "/game/data";
        public const string REtc = "/game/etc";
        public const string DEtc = "/game/data/etc";
        public const string D_SF = "/game/data/.syncfaction";
    }

    public static class Mod1
    {
        public const string Root = "/game/data/.syncfaction/Mod_1";
        public const string Data = "/game/data/.syncfaction/Mod_1/data";
        public const string REtc = "/game/data/.syncfaction/Mod_1/etc";
        public const string DEtc = "/game/data/.syncfaction/Mod_1/data/etc";
    }

    public static class Mod2
    {
        public const string Root = "/game/data/.syncfaction/Mod_2";
        public const string Data = "/game/data/.syncfaction/Mod_2/data";
        public const string REtc = "/game/data/.syncfaction/Mod_2/etc";
        public const string DEtc = "/game/data/.syncfaction/Mod_2/data/etc";
    }

    public static class Pch1
    {
        public const string Root = "/game/data/.syncfaction/Mod_66";
        public const string Data = "/game/data/.syncfaction/Mod_66/data";
        public const string REtc = "/game/data/.syncfaction/Mod_66/etc";
        public const string DEtc = "/game/data/.syncfaction/Mod_66/data/etc";
    }

    public static class Pch2
    {
        public const string Root = "/game/data/.syncfaction/Mod_77";
        public const string Data = "/game/data/.syncfaction/Mod_77/data";
        public const string REtc = "/game/data/.syncfaction/Mod_77/etc";
        public const string DEtc = "/game/data/.syncfaction/Mod_77/data/etc";
    }

    public static class BakV
    {
        public const string Root = "/game/data/.syncfaction/.bak_vanilla";
        public const string Data = "/game/data/.syncfaction/.bak_vanilla/data";
        public const string REtc = "/game/data/.syncfaction/.bak_vanilla/etc";
        public const string DEtc = "/game/data/.syncfaction/.bak_vanilla/data/etc";
    }

    public static class BakP
    {
        public const string Root = "/game/data/.syncfaction/.bak_patch";
        public const string Data = "/game/data/.syncfaction/.bak_patch/data";
        public const string REtc = "/game/data/.syncfaction/.bak_patch/etc";
        public const string DEtc = "/game/data/.syncfaction/.bak_patch/data/etc";
    }

    public static class Mngd
    {
        public const string Root = "/game/data/.syncfaction/.managed";
        public const string Data = "/game/data/.syncfaction/.managed/data";
        public const string REtc = "/game/data/.syncfaction/.managed/etc";
        public const string DEtc = "/game/data/.syncfaction/.managed/data/etc";
    }

    public static class Contents
    {
        public static class Orig
        {
            public const string Exe = "orig exe";
            public const string Dll = "orig dll";
            public const string Vpp = "orig vpp";
            public const string Txt = "orig txt";
            public const string Etc = "orig etc";
        }

        public static class Drty
        {
            public const string Exe = "drty exe";
            public const string Dll = "drty dll";
            public const string Vpp = "drty vpp";
            public const string Txt = "drty txt";
            public const string Etc = "drty etc";
        }

        public static class Mod1
        {
            public const string Exe = "mod1 exe";
            public const string Dll = "mod1 dll";
            public const string Vpp = "mod1 vpp";
            public const string Txt = "mod1 txt";
            public const string Etc = "mod1 etc";
        }

        public static class Mod2
        {
            public const string Exe = "mod2 exe";
            public const string Dll = "mod2 dll";
            public const string Vpp = "mod2 vpp";
            public const string Txt = "mod2 txt";
            public const string Etc = "mod2 etc";
        }

        public static class Pch1
        {
            public const string Exe = "pch1 exe";
            public const string Dll = "pch1 dll";
            public const string Vpp = "pch1 vpp";
            public const string Txt = "pch1 txt";
            public const string Etc = "pch1 etc";
        }

        public static class Pch2
        {
            public const string Exe = "pch2 exe";
            public const string Dll = "pch2 dll";
            public const string Vpp = "pch2 vpp";
            public const string Txt = "pch2 txt";
            public const string Etc = "pch2 etc";
        }
    }

    public static class Names
    {
        public const string Exe = "rfg____.exe";
        public const string Dll = "sw__api.dll";
        public const string Vpp = "misc.vpp_pc";
        public const string Txt = "text123.txt";
        public const string Etc = "file no ext";
    }


    public static MockFileSystem Init(Action<MockFileSystem>? action = null)
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(Game.Root);
        fs.AddDirectory(Game.Data);
        fs.AddDirectory(Game.D_SF);
        action?.Invoke(fs);
        return fs;
    }

    public static MockFileSystem Clone(this MockFileSystem src, Action<MockFileSystem>? action = null)
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

        public static IDictionary<string, string> ExeDll = MakeHashes(new[]
        {
            Names.Exe,
            Names.Dll
        });

        public static class Data
        {
            public static IDictionary<string, string> Vpp = MakeHashes(new[]
            {
                "data/" + Names.Vpp
            });
        }

        public static IDictionary<string, string> StockInAllDirs = MakeHashes(new[]
        {
            Names.Exe,
            "data/" + Names.Vpp,
            "etc/" + Names.Dll,
            "data/etc/" + Names.Etc,
        });

        public static IDictionary<string, string> MakeHashes(string[] fileNames) => new Dictionary<string, string>(fileNames.Select(InitHash));

        public static KeyValuePair<string, string> InitHash(string fileName) => new(fileName, $"hash of {fileName}");
    }

    public static void ShouldHaveSameFilesAs(this MockFileSystem actual, MockFileSystem expected)
    {
        // read all contents; remove "C:" and replace windows slashes to unix
        var actualDict = actual.AllFiles.ToDictionary(x => x.Substring(2).Replace('\\', '/'), x => actual.GetFile(x).TextContents);
        var expectedDict = expected.AllFiles.ToDictionary(x => x.Substring(2).Replace('\\', '/'), x => expected.GetFile(x).TextContents);

        actualDict.Should().BeEquivalentTo(expectedDict, because: SerializedInfo(actualDict, expectedDict));
    }

    public static TestFile TestFile(this MockFileSystem fs)
    {
        return new TestFile(fs);
    }

    public static GameFile GetGameFile(this MockFileSystem fs, IGameStorage storage, string path, string name)
    {
        return new GameFile(storage, fs.Path.Combine(path, name), fs);
    }

    /// <summary>
    /// Combines several dictionaries. Throws on duplicate keys
    /// </summary>
    public static IDictionary<T1, T2> Combine<T1, T2>(IDictionary<T1, T2>[] sources)
        where T1 : notnull =>
        sources.SelectMany(dict => dict)
            .ToLookup(pair => pair.Key, pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.Single());


    public record Row(string key, string exp, string act);

    public static string SerializedInfo(Dictionary<string, string> actual, Dictionary<string, string> expected)
    {
        /*return @$"
Actual:
{SerializeDictionary(actual)}
Expected:
{SerializeDictionary(expected)}
";*/

        string GetValue(Dictionary<string, string> d, string key)
        {
            if (d.TryGetValue(key, out var value))
            {
                return value == "" ? "(empty)" : value;
            }

            return " ░░░ ";  // visually highlight nonexistent files
        }

        var allKeys = actual.Keys.Concat(expected.Keys).Distinct();
        var table = allKeys
            .Select(x => new Row(x, GetValue(expected, x), GetValue(actual, x)))
            .ToList();
        var minLength = 10;
        var keyLength = table.Select(x => x.key.Length).Concat(new []{minLength}).Max();
        var actLength = table.Select(x => x.act.Length).Concat(new []{minLength}).Max();
        var expLength = table.Select(x => x.exp.Length).Concat(new []{minLength}).Max();
        var sb = new StringBuilder("ALL FILES:\n");

        void Serialize(Row x)
        {
            var key = x.key.PadRight(keyLength);
            var act = x.act.PadRight(actLength);
            var exp = x.exp.PadRight(expLength);
            var ok = x.act == x.exp ? "✓" : " ";
            sb.AppendLine($"{key}║{exp}║{act}║{ok}");
        }

        Serialize(new Row("PATH", $"EXPCTD {expected.Count}", $"ACTUAL {actual.Count}"));
        var groups = table.GroupBy(x => x.act == x.exp);
        foreach (var rows in groups.OrderBy(g => g.Key.ToString()))
        {
            sb.AppendLine(new string('═',keyLength+actLength+expLength+4));

            var sorted = rows
                .OrderBy(x => Path.GetDirectoryName(x.key))
                .ThenBy(x => Path.GetFileName(x.key))
                ;

            foreach (var x in sorted)
            {
                Serialize(x);
            }
        }

        sb.AppendLine(new string('═',keyLength+actLength+expLength+4));

        return sb.ToString();
    }

    public static TestFile MkFile(this MockFileSystem fs, string absPath, File file, Data data)
    {
        var f = new TestFile(fs);
        return f.Make(file, data).In(absPath);
    }
}

public class TestFile
{
    private readonly MockFileSystem fs;

    private MockFileData fileData = new MockFileData(string.Empty);

    private string name = string.Empty;
    private string fullPath = string.Empty;

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

    public void Delete()
    {
        fs.RemoveFile(fullPath);
    }

    public TestFile Make(File file, Data data)
    {
        this.name = file switch
        {
            File.Exe => Fs.Names.Exe,
            File.Dll => Fs.Names.Dll,
            File.Vpp => Fs.Names.Vpp,
            File.Txt => Fs.Names.Txt,
            File.Etc => Fs.Names.Etc,
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
        };

        this.fileData = data switch
        {
            SyncFactionTests.Data.None => string.Empty,
            SyncFactionTests.Data.Orig => file switch
            {
                File.Exe => Fs.Contents.Orig.Exe,
                File.Dll => Fs.Contents.Orig.Dll,
                File.Vpp => Fs.Contents.Orig.Vpp,
                File.Txt => Fs.Contents.Orig.Txt,
                File.Etc => Fs.Contents.Orig.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Drty => file switch
            {
                File.Exe => Fs.Contents.Drty.Exe,
                File.Dll => Fs.Contents.Drty.Dll,
                File.Vpp => Fs.Contents.Drty.Vpp,
                File.Txt => Fs.Contents.Drty.Txt,
                File.Etc => Fs.Contents.Drty.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Mod1 => file switch
            {
                File.Exe => Fs.Contents.Mod1.Exe,
                File.Dll => Fs.Contents.Mod1.Dll,
                File.Vpp => Fs.Contents.Mod1.Vpp,
                File.Txt => Fs.Contents.Mod1.Txt,
                File.Etc => Fs.Contents.Mod1.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Mod2 => file switch
            {
                File.Exe => Fs.Contents.Mod2.Exe,
                File.Dll => Fs.Contents.Mod2.Dll,
                File.Vpp => Fs.Contents.Mod2.Vpp,
                File.Txt => Fs.Contents.Mod2.Txt,
                File.Etc => Fs.Contents.Mod2.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Pch1 => file switch
            {
                File.Exe => Fs.Contents.Pch1.Exe,
                File.Dll => Fs.Contents.Pch1.Dll,
                File.Vpp => Fs.Contents.Pch1.Vpp,
                File.Txt => Fs.Contents.Pch1.Txt,
                File.Etc => Fs.Contents.Pch1.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            SyncFactionTests.Data.Pch2 => file switch
            {
                File.Exe => Fs.Contents.Pch2.Exe,
                File.Dll => Fs.Contents.Pch2.Dll,
                File.Vpp => Fs.Contents.Pch2.Vpp,
                File.Txt => Fs.Contents.Pch2.Txt,
                File.Etc => Fs.Contents.Pch2.Etc,
                _ => throw new ArgumentOutOfRangeException(nameof(file), file, null)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(data), data, null)
        };

        return this;
    }

    public TestFile In(string absPath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Place file after initializing name and data");
        }
        fullPath = fs.Path.Combine(absPath, name);
        fs.AddFile(fullPath, fileData);
        return this;
    }
}

public enum File
{
    Exe,
    Dll,
    Vpp,
    Txt,
    Etc,
}

public enum Data
{
    Orig,
    Drty,
    Mod1,
    Mod2,
    Pch1,
    Pch2,
    None
}