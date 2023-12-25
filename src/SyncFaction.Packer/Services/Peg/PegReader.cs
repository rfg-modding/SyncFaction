using Kaitai;
using SyncFaction.Packer.Models;
using SyncFaction.Packer.Models.Peg;

namespace SyncFaction.Packer.Services.Peg;

public class PegReader
{
    /// <summary>
    /// Initializes data streams from gpu (gpeg_pc) file and entry references. Cpu file can be closed, Gpu should remain open for later image reading
    /// </summary>
    public LogicalTextureArchive Read(Stream cpu, Stream gpu, string name, CancellationToken token)
    {
        var s = new KaitaiStream(cpu);
        var cpeg = new RfgCpeg(s);
        var textures = LogicalTextures(cpeg, gpu, token);

        return new LogicalTextureArchive(
            textures,
            name,
            (int) cpeg.Header.LenFileTotal,
            (int) cpeg.Header.LenData,
            cpeg.Header.AlignValue
        );
    }

    private static List<LogicalTexture> LogicalTextures(RfgCpeg cpeg, Stream gpu, CancellationToken token)
    {
        List<LogicalTexture> textures = new List<LogicalTexture>();

        if (cpeg.Header.NumEntries == 0)
        {
            return textures;
        }

        var i = 0;
        foreach (var entryData in cpeg.BlockEntryData.Value)
        {
            token.ThrowIfCancellationRequested();
            var x = cpeg.Entries[i];
            var texture = new LogicalTexture(
                new Size(x.Width, x.Height),
                new Size(x.SourceWidth, x.SourceHeight),
                new Size(x.AnimTilesWidth, x.AnimTilesHeight),
                x.Format,
                (TextureFlags) x.Flags,
                x.MipLevels,
                i,
                entryData.Name,
                (int) x.DataOffset,
                (int) x.NameOffset,
                cpeg.Header.AlignValue,
                new StreamView(gpu, entryData.DataOffset, entryData.DataSize)
            );
            textures.Add(texture);
            i++;
        }

        return textures;
    }
}
