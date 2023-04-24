using ICSharpCode.SharpZipLib;
using Kaitai;

namespace SyncFaction.Packer;

public class VppReader
{
    public LogicalArchive Read(Stream source, string name, CancellationToken token)
    {
        var s = new KaitaiStream(source);
        var vpp = new RfgVpp(s);
        var originalMode = vpp.Header.Flags.Mode;
        var data = ReadData(vpp, token);
        return new LogicalArchive(data, originalMode, name);
    }

    private IEnumerable<LogicalFile> ReadData(RfgVpp vpp, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!vpp.Entries.Any())
        {
            yield break;
        }

        Action? fixupAction = vpp.Header.Flags.Mode switch
        {
            RfgVpp.HeaderBlock.Mode.Compacted => () => vpp.ReadCompactedData(token),
            RfgVpp.HeaderBlock.Mode.Compressed => () => vpp.ReadCompressedData(token),
            RfgVpp.HeaderBlock.Mode.Condensed => throw new NotImplementedException("Condensed-only mode is not present in vanilla files and is not supported"),
            RfgVpp.HeaderBlock.Mode.Normal => null,
            _ => throw new ArgumentOutOfRangeException()
        };

        try
        {
            fixupAction?.Invoke();
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
            yield return new LogicalFile(entryData.Value.File, entryData.XName, entryData.I);
            entryData.DisposeAndFreeMemory();
        }
    }
}