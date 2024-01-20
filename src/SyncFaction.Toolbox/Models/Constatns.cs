using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace SyncFaction.Toolbox.Models;

public static class Constatns
{
    public const string DefaultDir = ".unpack";
    public const string DefaultOutputDir = ".output";
    public const string MetadataFile = ".metadata";

    /// <summary>
    /// Name format for new textures: "name format mipLevels.png". Example: "my_texture rgba_srgb 5.png"
    /// </summary>
    public static readonly Regex TextureNameFormat = new (@"^(?<name>.*?)\s+(?<format>.*?)\s+(?<mipLevels>\d+).png$", RegexOptions.Compiled);


    public static readonly ImmutableHashSet<string> KnownArchiveExtensions = new HashSet<string>
    {
        "vpp",
        "vpp_pc",
        "str2",
        "str2_pc"
    }.ToImmutableHashSet();

    public static readonly ImmutableHashSet<string> KnownTextureArchiveExtensions = new HashSet<string>
    {
        "cpeg_pc",
        "cvbm_pc",
        "gpeg_pc",
        "gvbm_pc",
    }.ToImmutableHashSet();
}
