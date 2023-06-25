namespace SyncFaction.ModManager.Models;

/// <param name="Archive">Path to vpp like "data/foo.vpp_pc"</param>
/// <param name="File">Path to a file inside vpp: foo/bar/baz.str2</param>
public record VppPath(string Archive, string File);
