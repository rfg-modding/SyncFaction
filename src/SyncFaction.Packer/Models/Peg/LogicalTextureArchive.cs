using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SyncFaction.Packer.Models.Peg;

public record LogicalTextureArchive(
    List<LogicalTexture> LogicalTextures,
    string Name,
    int TotalLength,
    int DataLength,
    int Align
)
{
    // TODO: append

    public void Replace(string name, Image<Rgba32> newImage)
    {
        throw new NotImplementedException("TODO: find dds encoder and figure out parameters");
        var existing = LogicalTextures.Single(x => x.Name == name);
        //existing.

    }
}
