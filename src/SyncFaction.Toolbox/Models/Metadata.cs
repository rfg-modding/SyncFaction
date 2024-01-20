using System.Globalization;
using System.Text;

namespace SyncFaction.Toolbox.Models;

internal class Metadata : SortedDictionary<string, ArchiveMetadata>
{
    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var (key, (name, mode, size, entryCount, hash, metaEntries)) in this)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{key}\t\t{mode}, {entryCount} entries, {size} bytes, {hash}");
            foreach (var (eKey, (eName, order, offset, eSize, compressedSize, eHash)) in metaEntries)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{key}\\{eKey}\t\t{order}, {eSize} bytes, {eHash}");
            }
        }

        return sb.ToString();
    }
}
