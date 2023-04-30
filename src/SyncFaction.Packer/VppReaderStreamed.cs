using ICSharpCode.SharpZipLib;
using Kaitai;

namespace SyncFaction.Packer;

public class VppReaderStreamed
{
    public LogicalArchiveStreamed Read(Stream source, string name, CancellationToken token)
    {
        var s = new KaitaiStream(source);
        var vpp = new RfgVppStreamed(s);
        var originalMode = vpp.Header.Flags.Mode;
        var data = ReadData(vpp, token);
        return new LogicalArchiveStreamed(data, originalMode, name);
    }

    private IEnumerable<LogicalFileStreamed> ReadData(RfgVppStreamed vpp, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!vpp.Entries.Any())
        {
            yield break;
        }

        Action<CancellationToken> fixupAction = vpp.Header.Flags.Mode switch
        {
            RfgVppStreamed.HeaderBlock.Mode.Normal => _ => { },
            RfgVppStreamed.HeaderBlock.Mode.Compacted => vpp.ReadCompactedData,
            RfgVppStreamed.HeaderBlock.Mode.Compressed => vpp.ReadCompressedData,
            RfgVppStreamed.HeaderBlock.Mode.Condensed => throw new InvalidOperationException("Condensed-only mode is not present in vanilla files and is not supported"),
            _ => throw new ArgumentOutOfRangeException()
        };

        try
        {
            fixupAction.Invoke(token);
        }
        catch (SharpZipBaseException e)
        {
            var streamSize = "(unknown)";
            if (vpp.M_Io.BaseStream is MemoryStream ms)
            {
                streamSize = ms.Length.ToString();
            }
            var msg = $"Failed to unzip data. Stream size = [{streamSize}]. Header = [{vpp.Header}]";
            throw new InvalidOperationException(msg, e);
        }

        foreach (var entryData in vpp.BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            yield return new LogicalFileStreamed(entryData.GetDataStream(), entryData.XName, entryData.I);
        }
    }
}
