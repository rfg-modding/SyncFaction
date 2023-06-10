using System.Security.Cryptography;
using Newtonsoft.Json;
using SyncFaction.Core.Data;

namespace SyncFactionTests;

public static class TestUtils
{

    /// <summary>
    /// To populate files from inside initial vpp archives, run "UnpackNested" test manually
    /// </summary>
    public static IEnumerable<TestCaseData> AllArchiveFiles()
    {
        return AllVppFiles().Concat(AllExtractedStr2Once);
    }

    public static IEnumerable<TestCaseData> AllVppFiles()
    {
        return AllVppFilesOnce
                //.Where(x => ((FileInfo) x.OriginalArguments[0]).Name.Contains(@"table.vpp_pc"))
                .OrderBy(x => ((FileInfo) x.OriginalArguments[0]).Length)
                //.Skip(10)
            ;
    }

    public static IEnumerable<TestCaseData> AllFiles()
    {
        return AllVppFilesOnce.Concat(AllExtractedOnce);
    }

    public static IEnumerable<TestCaseData> AllModInfos()
    {
        return AllModInfosOnce;
    }

    public const string DefaultPath = @"C:\Program Files (x86)\GOG Galaxy\Games\Red Faction Guerrilla Re-Mars-tered";
    public static readonly DirectoryInfo ArtifactDir = new(Path.Combine(DefaultPath, @"data\.syncfaction\test_artifacts"));
    public static readonly DirectoryInfo UnpackDir = new(Path.Combine(DefaultPath, @"data\.syncfaction\test_artifacts\unpack"));

    private static List<TestCaseData> AllExtractedOnce = InitAllExtractedOnce();

    private static readonly IEnumerable<TestCaseData> AllVppFilesOnce = Hashes.Vpp
        .Concat(Hashes.Gog)
        .Select(x => x.Key)
        .Where(x => x.ToLowerInvariant().EndsWith(".vpp_pc"))
        //.Where(x => x.Contains("table") || x.Contains("anims"))
        .OrderBy(x => x)
        .Select(x => Path.Combine(DefaultPath, x))
        .Select(x => new FileInfo(x))
        .Select(x => new TestCaseData(x).SetArgDisplayNames(x.Name))
        .ToList();

    private static List<TestCaseData> AllExtractedStr2Once = InitAllExtractedStr2Once();

    private static List<TestCaseData> AllModInfosOnce = InitAllModInfoOnce();


    private static List<TestCaseData> InitAllExtractedOnce()
    {
        UnpackDir.Create();
        return UnpackDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(UnpackDir.FullName.Length + 1)))
            .ToList();
    }

    private static List<TestCaseData> InitAllExtractedStr2Once()
    {
        UnpackDir.Create();
        return UnpackDir.EnumerateFiles("*.str2_pc", SearchOption.AllDirectories)
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(UnpackDir.FullName.Length + 1)))
            .ToList();
    }

    private static List<TestCaseData> InitAllModInfoOnce()
    {
        var modsDir = new DirectoryInfo(Path.Combine(DefaultPath, "mods"));
        return  modsDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(x => x.Name.ToLower() == "modinfo.xml")
            .OrderBy(x => x.FullName)
            .Select(x => new TestCaseData(x).SetArgDisplayNames(x.FullName.Substring(modsDir.FullName.Length + 1)))
            .ToList();
    }

    public static readonly IReadOnlyDictionary<string, byte[]> Signatures = new Dictionary<string, byte[]>()
    {
        {".vpp_pc", BitConverter.GetBytes(1367935694u)},
        {".str2_pc", BitConverter.GetBytes(1367935694u)},
        {".layer_pc", BitConverter.GetBytes(1162760026u)},
        {".anim_pc", BitConverter.GetBytes(1296649793u)},
        {".asm_pc", BitConverter.GetBytes(3203399405u)},
        {".cchk_pc", new byte[]{0xA5, 0xEF}},
        {".mat_pc", BitConverter.GetBytes(2954754766u)},
        {".ctmesh_pc", BitConverter.GetBytes(1514296659u)},
        {".cterrain_pc", BitConverter.GetBytes(1381123412u)},
        {".csmesh_pc", BitConverter.GetBytes(3237998097u)},
        {".ccmesh_pc", BitConverter.GetBytes(4207104425u)},
        {".fxo_kg", new byte[]{0xEE, 0xA1}},
        {".ccar_pc", BitConverter.GetBytes(28u)},
        {".vint_doc", BitConverter.GetBytes(12327u)},
        {".cfmesh_pc", BitConverter.GetBytes(267501985u)},
        {".cstch_pc", BitConverter.GetBytes(2966351781u)},
        {".rfgvp_pc", BitConverter.GetBytes(1868057136u)},
        {".vfdvp_pc", BitConverter.GetBytes(1346651734u)},
        {".rfglocatext", new byte[]{0x73, 0x7F}},
    };

    public static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashValue = sha.ComputeHash(stream);
        var hash = BitConverter.ToString(hashValue).Replace("-", "");
        return hash;
    }

    public static void PrintJson(object value)
    {
        // STJ does not support polymorphic serialization!
        //var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(){WriteIndented = true});
        var json = JsonConvert.SerializeObject(value, Formatting.Indented);
        Console.WriteLine(json);
    }
}
