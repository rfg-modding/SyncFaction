using FluentAssertions;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Kaitai;
using SyncFaction.Core.Data;

namespace SyncFactionTests;

public class TestUtils
{
    public static IEnumerable<TestCaseData> AllVppFiles()
    {
        /*var path = @"C:\Program Files (x86)\Steam\steamapps\common\Red Faction Guerrilla Re-MARS-tered\data";
        var dir = new DirectoryInfo(path);
        return dir.EnumerateFiles("*.vpp_pc").OrderBy(x => x.Name).Select(x => new TestCaseData(x).SetName(x.Name));*/
        return AllVppFilesOnce;
    }

    private const string  DefaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Red Faction Guerrilla Re-MARS-tered";

    private static readonly IEnumerable<TestCaseData> AllVppFilesOnce = Hashes.Vpp
        .Select(x => x.Key)
        .OrderBy(x => x)
        .Select(x => Path.Combine(DefaultPath, x))
        .Select(x => new FileInfo(x))
        .Select(x => new TestCaseData(x).SetArgDisplayNames(x.Name))
        .ToList();

    private static long CheckOffset(RfgVpp.EntryData entryData, long unpackedOffset, string description)
    {
        // this is offset AFTER UNPACKING but WITH ALIGNMENT
        entryData.XDataOffset.Should().Be((uint) unpackedOffset, description);
        var unpackedPadSize = entryData.XLenData == 0 ? 0 : 2048 - (entryData.XLenData % 2048);
        unpackedOffset += entryData.XLenData + unpackedPadSize;
        return unpackedOffset;
    }

    private static void CheckHash(RfgVpp.EntryData entryData)
    {
        var hash = CircularHash(entryData.XName);
        var s = $"{entryData.XName} {Tools.HexString(entryData.XNameHash)} {Tools.HexString(entryData.XNameHash)}\n";
        hash.Should().BeEquivalentTo(entryData.XNameHash, entryData.ToString(), s);
    }

    public static byte[] CircularHash(string input)
    {
        input = input.ToLowerInvariant();

        uint hash = 0;
        for (int i = 0; i < input.Length; i++)
        {
            // rotate left by 6
            hash = (hash << 6) | (hash >> (32 - 6));
            hash = input[i] ^ hash;
        }
        var result = hash;
        return BitConverter.GetBytes(result);
    }




}
