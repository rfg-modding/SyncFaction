using System.Runtime.InteropServices;
using DirectXTexNet;
using Pfim;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SyncFaction.Packer.Models.Peg;
using Image = DirectXTexNet.Image;


namespace SyncFaction.Packer.Services.Peg;

public class ImageConverter
{
    public async Task WritePngFile(Image<Bgra32> image, Stream destination, CancellationToken token)
    {
        var encoder = new PngEncoder();
        await image.SaveAsync(destination, encoder, token);
    }

    public async Task<Image<Bgra32>> ReadPngFile(FileInfo source, CancellationToken token)
    {
        await using var s = source.OpenRead();
        return await PngDecoder.Instance.DecodeAsync<Bgra32>(new PngDecoderOptions(), s, token);
    }

    public void Encode(Image<Bgra32> png, LogicalTexture logicalTexture)
    {

    }

    public unsafe Image<Bgra32> Decode(LogicalTexture logicalTexture)
    {
        var intPtr = Marshal.AllocHGlobal(logicalTexture.TotalSize);
        var bytePtr = (byte*) intPtr.ToPointer();
        using (var writeStream = new UnmanagedMemoryStream(bytePtr, logicalTexture.TotalSize, logicalTexture.TotalSize, FileAccess.Write))
        {
            logicalTexture.Data.CopyTo(writeStream);
        }

        try
        {
            DXGI_FORMAT format = GetDxFormat(logicalTexture.Format, logicalTexture.Flags);
            long rowPitch = GetRowPitch(format, logicalTexture.Size.Width);
            var dxImage = new Image(logicalTexture.Size.Width, logicalTexture.Size.Height, format, rowPitch, logicalTexture.TotalSize, intPtr, null);
            if (logicalTexture.Flags.HasFlag(TextureFlags.CubeTexture))
            {
                throw new NotImplementedException("TODO is this a cubemap? TexMetadata should have arraySize=6, or 6 * numCubes if we expect cubemap array (idk what that is)");
            }
            // TODO should we do something about mipmaps?
            var metadata = new TexMetadata(logicalTexture.Size.Width, logicalTexture.Size.Height, 1, 1, 1, 0, 0, format, TEX_DIMENSION.TEXTURE2D);
            using var scratchImage = TexHelper.Instance.InitializeTemporary(new[] {dxImage}, metadata);
            // TODO check what format ends up in dds
            using var dds = scratchImage.SaveToDDSMemory(DDS_FLAGS.NONE);
            using var pfImage = Pfimage.FromStream(dds);
            var tightStride = pfImage.Width * pfImage.BitsPerPixel / 8;
            if (pfImage.Stride != tightStride)
            {
                throw new NotImplementedException("See workaround at https://github.com/nickbabcock/Pfim/blob/master/src/Pfim.ImageSharp/Program.cs");
            }

            return pfImage.Format switch
            {
                ImageFormat.Rgba32 => SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(pfImage.Data, pfImage.Width, pfImage.Height),
                _ => throw new ArgumentOutOfRangeException(nameof(pfImage.Format), pfImage.Format, "See workaround at https://github.com/nickbabcock/Pfim/blob/master/src/Pfim.ImageSharp/Program.cs")
            };
        }
        finally
        {
            Marshal.FreeHGlobal(intPtr);
        }
    }



    private DXGI_FORMAT GetDxFormat(RfgCpeg.Entry.BitmapFormat format, TextureFlags flags) =>
        format switch
        {
            RfgCpeg.Entry.BitmapFormat.PcDxt1 => flags.HasFlag(TextureFlags.Srgb) ? DXGI_FORMAT.BC1_UNORM_SRGB : DXGI_FORMAT.BC1_UNORM,
            RfgCpeg.Entry.BitmapFormat.PcDxt5 => flags.HasFlag(TextureFlags.Srgb) ? DXGI_FORMAT.BC3_UNORM_SRGB : DXGI_FORMAT.BC3_UNORM,
            RfgCpeg.Entry.BitmapFormat.Pc8888 => flags.HasFlag(TextureFlags.Srgb) ? DXGI_FORMAT.R8G8B8A8_UNORM_SRGB : DXGI_FORMAT.R8G8B8A8_UNORM,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported texture format")
        };

    private int GetRowPitch(DXGI_FORMAT dxgiFormat, int width)
    {
        return dxgiFormat switch
        {
            DXGI_FORMAT.BC1_UNORM or DXGI_FORMAT.BC1_UNORM_SRGB => 8 * (width / 4),
            DXGI_FORMAT.BC3_UNORM or DXGI_FORMAT.BC3_UNORM_SRGB => 16 * (width / 4),
            DXGI_FORMAT.R8G8B8A8_UNORM_SRGB or DXGI_FORMAT.R8G8B8A8_UNORM => 4 * width,
            _ => throw new ArgumentOutOfRangeException(nameof(dxgiFormat), dxgiFormat, "Unsupported texture format")
        };
    }
}

/*TODO

 * calculate stats of existing formats: bc1/2/3/rgb8, srgb or not, compression, flags, whatever
 * convert directXtex image to dumb format readable by imagesharp
 * opposite: imagesharp to dumb format to dxtex in required format
 * is space used in filenames?

*/
