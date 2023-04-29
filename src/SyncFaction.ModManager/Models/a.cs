using System.Collections.Immutable;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Xml;

namespace SyncFaction.ModManager.Models;

public record ModInfoOperations(IReadOnlyList<FileSwapOperation> FileSwaps, IReadOnlyList<XmlEditOperation> XmlEdits)
{
    public IReadOnlyDictionary<string, VppOperations> VppOperations { get; init; } = GroupByVpp(FileSwaps, XmlEdits);

    public static IReadOnlyDictionary<string, VppOperations> GroupByVpp(IReadOnlyList<FileSwapOperation> fileSwaps, IReadOnlyList<XmlEditOperation> xmlEdits)
    {
        var allArchives = fileSwaps.Select(x => x.VppPath.Archive)
            .Concat(xmlEdits.Select(x => x.VppPath.Archive))
            .Distinct();

        var swaps = fileSwaps.ToLookup(x => x.VppPath.Archive);
        var edits = xmlEdits.ToLookup(x => x.VppPath.Archive);

        return allArchives.ToDictionary(v => v, v =>
        {
            var sw = swaps[v].ToImmutableDictionary(x => x.VppPath.File);
            var ed = edits[v].ToImmutableDictionary(x => x.VppPath.File);
            return new VppOperations(sw, ed);
        });
    }
}

public record VppOperations(IImmutableDictionary<string, FileSwapOperation> FileSwaps, IImmutableDictionary<string, XmlEditOperation> XmlEdits);

public interface IOperation
{
    public int Index { get; }
    public VppPath VppPath { get; }
}

public record FileSwapOperation(int Index, VppPath VppPath, IFileInfo Target) : IOperation;

// TODO enum Action
public record XmlEditOperation(int Index, VppPath VppPath, string Action, List<XmlNode> Xml) : IOperation;

/// <param name="Archive">Path to vpp like "data/foo.vpp_pc"</param>
/// <param name="File">Path to a file inside vpp: foo/bar/baz.str2</param>
public record VppPath(string Archive, string File);
