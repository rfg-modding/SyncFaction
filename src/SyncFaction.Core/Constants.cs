using System.Collections.Immutable;

namespace SyncFaction.Core;

public static class Constants
{
    public const string BakDirName = ".bak_vanilla";
    public const string PatchBakDirName = ".bak_patch";
    public const string ManagedDirName = ".managed";
    public const string AppDirName = ".syncfaction";
    public const string ImgDirName = ".img";
    public const string StateFile = "state.txt";
    public const string ModDescriptionFile = ".mod.json";
    public const string HashFile = ".mod.hashes.json";
    public const string SteamModDir = ".steam";
    public const string GogModDir = ".gog";
    public const string ModInfoFile = "modinfo.xml";
    public const int LogEventId = 42;

    /// <summary>
    /// This file is written to mod dir as a marker of ongoing downloading and unpacking.
    /// If it exists, last attempt was failed (user closed app, exception occured) and files are damaged
    /// </summary>
    public const string IncompleteDataFile = ".mod.incomplete_data";

    public const string ApiUrl = @"https://autodl.factionfiles.com/rfg/v1/files-by-cat.php";
    public const string FindMapUrl = @"https://autodl.factionfiles.com/findmap.php";
    public const string BrowserUrlTemplate = "https://www.factionfiles.com/ff.php?action=file&id={0}";

    public const string WikiPage = "https://www.redfactionwiki.com/wiki/RF:G_Game_Night_News";

    public const string PatchSearchStringPrefix = "rfgterraform";
    public const string RslSearchStringPrefix = "rfgscriptloader";

    public const string CdnUrl = @"https://rfg.rast.rocks";
    public const string CdnListUrl = @"https://storage.bunnycdn.com/rfgmods/dev/"; // NOTE: trailing slash is important!
    public const string CdnReadApiKey = "8c96ec05-2d18-4ed6-83f1bbf5a112-f128-4744";

    /// <summary>
    /// Types to skip when processing mod files
    /// </summary>
    public static readonly ImmutableHashSet<string> IgnoredExtensions = new HashSet<string>
    {
        ".rfgpatch",
        ".txt",
        ".jpg",
        ".jpeg",
        ".png",
        ".zip",
        ".rar",
        ".7z"
    }.ToImmutableHashSet();
}
