namespace SyncFaction.Core.Data;

public static class Constants
{
    public const string BakDirName = ".bak_vanilla";
    public const string CommunityBakDirName = ".bak_community";
    public const string AppDirName = ".syncfaction";
    public const string ImgDirName = ".img";
    public const string StateFile = "state.txt";
    public const string ModDescriptionFile = ".mod.json";
    public const string ModInfoFile = "modinfo.xml";

    /// <summary>
    /// This file is written to mod dir as a marker of ongoing downloading and unpacking.
    /// If it exists, last attempt was failed (user closed app, exception occured) and files are damaged
    /// </summary>
    public const string IncompleteDataFile = ".mod.incomplete_data";

    public const string ApiUrl = @"https://autodl.factionfiles.com/rfg/v1/files-by-cat.php";
    public const string FindMapUrl = @"https://autodl.factionfiles.com/findmap.php";
    public const string BrowserUrlTemplate = "https://www.factionfiles.com/ff.php?action=file&id={0}";
    public const string WikiPage = "https://www.redfactionwiki.com/wiki/RF:G_Game_Night_News";
    public const string CdnUrl = @"https://rfg.rast.rocks";
    public const string CdnListUrl = @"https://storage.bunnycdn.com/rfgmods/dev/";  // NOTE: trailing slash is important!
    public const string CdnReadApiKey = "8c96ec05-2d18-4ed6-83f1bbf5a112-f128-4744";

    public static readonly string ErrorFormat = @"# Error!
**Operation failed**: {0}

## What now
+ **Steam** : check files integrity or reinstall game
+ **GOG** : reinstall game
+ Check if game location is valid
+ See if new versions of SyncFaction are available on Github
+ Please report this error to developer. **Copy all the stuff below** to help fixing it!
+ Take care of your sledgehammer and remain a good Martian

{1}

";

}
