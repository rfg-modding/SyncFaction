namespace SyncFaction.ModManager.Models;

public record ModInfoOperations(IReadOnlyList<FileSwapOperation> FileSwaps, IReadOnlyList<XmlEditOperation> XmlEdits)
{
    public IReadOnlyDictionary<string, VppOperations> VppOperations { get; init; } = GroupByVpp(FileSwaps, XmlEdits);

    private static IReadOnlyDictionary<string, VppOperations> GroupByVpp(IReadOnlyList<FileSwapOperation> fileSwaps, IReadOnlyList<XmlEditOperation> xmlEdits)
    {
        var allArchives = fileSwaps.Select(static x => x.VppPath.Archive).Concat(xmlEdits.Select(x => x.VppPath.Archive)).Distinct();

        var swaps = fileSwaps.ToLookup(static x => x.VppPath.Archive);
        var edits = xmlEdits.ToLookup(static x => x.VppPath.Archive);

        return allArchives.ToDictionary(static v => v,
            v =>
            {
                var sw = swaps[v].ToLookup(static x => x.VppPath.File);
                var ed = edits[v].ToLookup(static x => x.VppPath.File);
                return new VppOperations(sw, ed);
            });
    }
}
