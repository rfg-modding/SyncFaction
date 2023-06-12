using System.Collections.Immutable;
using System.ComponentModel;
using System.Xml.Serialization;

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
            var sw = swaps[v].ToLookup(x => x.VppPath.File);
            var ed = edits[v].ToLookup(x => x.VppPath.File);
            return new VppOperations(sw, ed);
        });
    }
}
