using FluentAssertions;
using Kaitai;
using SyncFaction.Core.Data;

namespace SyncFactionTests;

public class TestUtils
{
    public static IEnumerable<TestCaseData>  AllVppFiles()
    {
        return AllVppFilesOnce.Concat(AllExtractedStr2Once);
    }

    public const string  DefaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Red Faction Guerrilla Re-MARS-tered";
    public const string  ExtractionDir = DefaultPath + @"\data\.syncfaction\packer_tests";

    private static List<TestCaseData> AllExtractedStr2Once = InitAllExtractedStr2Once();

    private static List<TestCaseData> InitAllExtractedStr2Once()
    {
        var dir = new DirectoryInfo(ExtractionDir);
        dir.Create();
        return dir.EnumerateFiles("*.str2_pc", SearchOption.AllDirectories)
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(ExtractionDir.Length+1)))
            .ToList();
    }


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
