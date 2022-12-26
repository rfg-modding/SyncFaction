using System.Text.Json;
using FluentAssertions;
using Kaitai;
using SyncFaction.Packer;

namespace SyncFactionTests.Packer;

[Explicit("Depend on paths tied to steam version")]
public class HashTests
{

    [TestCaseSource(typeof(TestUtils), nameof(TestUtils.AllVppFiles))]
    public void CalculateHashes(FileInfo fileInfo)
    {
        var key = fileInfo.Name;
        using var fileStream = fileInfo.OpenRead();
        CheckHashRecursive(fileStream, key, null);
    }

    public void CheckHashRecursive(Stream stream, string name, string? parentKey)
    {
        var key = parentKey == null ? name : $"{parentKey}/{name}";
        //Console.WriteLine($"hash {key}");

        var hashString = TestUtils.ComputeHash(stream);
        stream.Position = 0;

        // archives themselves can be different, its ok
        if (!(name.EndsWith(".str2_pc") || name.EndsWith(".vpp_pc")))
        {
            Console.WriteLine($"chck {key}");
            var expected = GetHash(key);
            hashString.Should().Be(expected, $"Key = {key}");
        }

        if (name.EndsWith(".str2_pc") || name.EndsWith(".vpp_pc"))
        {
            //Console.WriteLine($"read {key}");
            var entries = Tools.UnpackVpp(stream, key).LogicalFiles;
            foreach (var entry in entries)
            {
                using var ms = new MemoryStream(entry.Content);
                CheckHashRecursive(ms, entry.Name, key);
            }
        }
    }

    [OneTimeSetUp]
    public void LoadHashes()
    {
        var file = new FileInfo(Path.Combine(Environment.CurrentDirectory, "hashes.json"));
        using var s = file.OpenRead();
        using var sr = new StreamReader(s);
        var text = sr.ReadToEnd();
        AllHashes = JsonSerializer.Deserialize<Dictionary<string, string>>(text)!;
    }

    private string GetHash(string key)
    {
        return AllHashes[key].Split(" ")[0];
    }

    public static Dictionary<string,string> AllHashes = new();
}
