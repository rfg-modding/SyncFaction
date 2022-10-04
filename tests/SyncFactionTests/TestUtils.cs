using FluentAssertions;
using Kaitai;
using SyncFaction.Core.Data;
using SyncFaction.Packer;

namespace SyncFactionTests;

public static class TestUtils
{
    /// <summary>
    /// To populate files from inside initial vpp archives, run "UnpackNested" test manually
    /// </summary>
    public static IEnumerable<TestCaseData> AllArchiveFiles()
    {
        return AllVppFilesOnce.Concat(AllExtractedStr2Once);
    }

    public static IEnumerable<TestCaseData> AllFiles()
    {
        return AllVppFilesOnce.Concat(AllExtractedOnce);
    }

    public const string DefaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Red Faction Guerrilla Re-MARS-tered";
    public const string ExtractionDir = DefaultPath + @"\data\.syncfaction\packer_tests";

    private static List<TestCaseData> AllExtractedStr2Once = InitAllExtractedStr2Once();

    private static List<TestCaseData> AllExtractedOnce = InitAllExtractedOnce();

    private static List<TestCaseData> InitAllExtractedStr2Once()
    {
        var dir = new DirectoryInfo(ExtractionDir);
        dir.Create();
        return dir.EnumerateFiles("*.str2_pc", SearchOption.AllDirectories)
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(ExtractionDir.Length + 1)))
            .ToList();
    }

    private static List<TestCaseData> InitAllExtractedOnce()
    {
        var dir = new DirectoryInfo(ExtractionDir);
        dir.Create();
        return dir.EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(ExtractionDir.Length + 1)))
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

    public static readonly IReadOnlyDictionary<string, byte[]> Signatures = new Dictionary<string, byte[]>()
    {
        {".vpp_pc", BitConverter.GetBytes(1367935694u)},
        {".str2_pc", BitConverter.GetBytes(1367935694u)},
        {".layer_pc", BitConverter.GetBytes(1162760026u)},
        {".anim_pc", BitConverter.GetBytes(1296649793u)},
        {".asm_pc", BitConverter.GetBytes(3203399405u)},
        {".cchk_pc", new byte[]{0x78, 0xDA}},
        {".mat_pc", BitConverter.GetBytes(2954754766u)},
        {".ctmesh_pc", BitConverter.GetBytes(1514296659u)},
        {".cterrain_pc", BitConverter.GetBytes(1381123412u)},
        {".csmesh_pc", BitConverter.GetBytes(3237998097u)},
        {".ccmesh_pc", BitConverter.GetBytes(4207104425u)},
        {".fxo_kg", new byte[]{0x78, 0x01}},
        {".ccar_pc", BitConverter.GetBytes(28u)},
        {".vint_doc", BitConverter.GetBytes(12327u)},
        {".cfmesh_pc", BitConverter.GetBytes(267501985u)},
        {".cstch_pc", BitConverter.GetBytes(2966351781u)},
        {".rfgvp_pc", BitConverter.GetBytes(1868057136u)},
        {".vfdvp_pc", BitConverter.GetBytes(1346651734u)},
        {".rfglocatext", new byte[]{0x78, 0x01}},
    };
}
