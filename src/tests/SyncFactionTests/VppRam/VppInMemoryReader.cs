using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ICSharpCode.SharpZipLib;
using Kaitai;

namespace SyncFactionTests.VppRam;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Tests")]
public class VppInMemoryReader
{
    public LogicalInMemoryArchive Read(Stream source, string name, CancellationToken token)
    {
        var s = new KaitaiStream(source);
        var vpp = new RfgVppInMemory(s);
        var originalMode = vpp.Header.Flags.Mode;
        var data = ReadData(vpp, token);
        return new LogicalInMemoryArchive(data, originalMode, name);
    }

    private IEnumerable<LogicalInMemoryFile> ReadData(RfgVppInMemory vppInMemory, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!vppInMemory.Entries.Any())
        {
            yield break;
        }

        Action<CancellationToken> fixupAction = vppInMemory.Header.Flags.Mode switch
        {
            RfgVppInMemory.HeaderBlock.Mode.Normal => _ => { },
            RfgVppInMemory.HeaderBlock.Mode.Compacted => vppInMemory.ReadCompactedData,
            RfgVppInMemory.HeaderBlock.Mode.Compressed => vppInMemory.ReadCompressedData,
            RfgVppInMemory.HeaderBlock.Mode.Condensed => throw new InvalidOperationException("Condensed-only mode is not present in vanilla files and is not supported"),
            _ => throw new ArgumentOutOfRangeException()
        };

        try
        {
            fixupAction.Invoke(token);
        }
        catch (SharpZipBaseException e)
        {
            var streamSize = "(unknown)";
            if (vppInMemory.M_Io.BaseStream is MemoryStream ms)
            {
                streamSize = ms.Length.ToString(CultureInfo.InvariantCulture);
            }

            var msg = $"Failed to unzip data. Stream size = [{streamSize}]. Header = [{vppInMemory.Header}]";
            throw new InvalidOperationException(msg, e);
        }

        foreach (var entryData in vppInMemory.BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            yield return new LogicalInMemoryFile(entryData.Value.File, entryData.XName, entryData.I, entryData.ToString());

            entryData.DisposeAndFreeMemory();
        }
    }
}
