using DirectXTexNet;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SyncFaction.Packer.Models.Peg;
using Image = DirectXTexNet.Image;


namespace SyncFaction.Packer.Services.Peg;

public class ImageConverter
{
    private readonly ILogger<ImageConverter> log;

    public ImageConverter(ILogger<ImageConverter> log)
    {
        this.log = log;
    }

    public async Task WritePngFile(Image<Rgba32> image, Stream destination, CancellationToken token)
    {
        var encoder = new PngEncoder();
        await image.SaveAsync(destination, encoder, token);
    }

    public async Task<Image<Rgba32>> ReadPngFile(FileInfo source, CancellationToken token)
    {
        await using var s = source.OpenRead();
        return await PngDecoder.Instance.DecodeAsync<Rgba32>(new PngDecoderOptions(), s, token);
    }

    public unsafe Stream Encode(Image<Rgba32> png, LogicalTexture logicalTexture)
    {
        using var disposables = new Disposables();
        var pngSize = png.Width * png.Height * (png.PixelType.BitsPerPixel / 8);
        var pointer = new DisposablePtr(pngSize);
        disposables.Add(pointer);

        var span = new Span<byte>(pointer.value.ToPointer(), pngSize);
        png.CopyPixelDataTo(span);
        var initialFormat = DXGI_FORMAT.R8G8B8A8_UNORM;
        TexHelper.Instance.ComputePitch(initialFormat, png.Width, png.Height, out var rowPitch, out _, CP_FLAGS.NONE);
        var dxImage = new Image(png.Width, png.Height, initialFormat, rowPitch, pngSize, pointer.value, null);
        var metadata = new TexMetadata(png.Width, png.Height, 1, 1, 1, 0, 0, initialFormat, TEX_DIMENSION.TEXTURE2D);
        var scratchImage = TexHelper.Instance.InitializeTemporary(new[] {dxImage}, metadata);
        disposables.Add(scratchImage);
        if (logicalTexture.MipLevels > 1)
        {
            scratchImage = scratchImage.GenerateMipMaps(TEX_FILTER_FLAGS.DEFAULT, logicalTexture.MipLevels);
            disposables.Add(scratchImage);
            log.LogDebug("DDS bytes with generated mipmaps: {pixels}", scratchImage.GetPixelsSize());
        }
        var (dxFormat, compressed, _, _) = GetDxFormat(logicalTexture.Format, logicalTexture.Flags);
        if (compressed)
        {
            scratchImage = scratchImage.Compress(dxFormat, TEX_COMPRESS_FLAGS.DEFAULT, 0.5f);
            disposables.Add(scratchImage);
        }

        if (dxFormat == DXGI_FORMAT.R8G8B8A8_UNORM_SRGB)
        {
            scratchImage = scratchImage.Convert(DXGI_FORMAT.R8G8B8A8_UNORM_SRGB, TEX_FILTER_FLAGS.DEFAULT, 0.5f);
            disposables.Add(scratchImage);
        }

        var dxSize = (int)scratchImage.GetPixelsSize(); // total length with all mips
        log.LogDebug("DDS bytes after compression: {pixels}", dxSize);
        // TODO pad to 16 always?
        var dataAlign = 16;
        var remainder = dxSize % dataAlign;
        var padding = remainder > 0 ? dataAlign - remainder : 0;
        var totalSize = dxSize + padding;
        log.LogDebug("DDS DATA padding {padding}, totalSize {totalSize}", padding, totalSize);
        // TODO maybe return unmanaged memory stream and make 1 less copy?
        var pixels = scratchImage.GetPixels();
        var pixelSpan = new Span<byte>(pixels.ToPointer(), dxSize);
        var result = new MemoryStream(dxSize);
        result.Write(pixelSpan);
        var padSpan = new byte[padding].AsSpan();
        padSpan.Fill(0);
        result.Write(padSpan);
        result.Position = 0;
        return result;
    }

    public unsafe Image<Rgba32> DecodeFirstFrame(LogicalTexture logicalTexture)
    {
        using var disposables = new Disposables();
        var header = BuildHeader(logicalTexture, CancellationToken.None).Result;
        disposables.Add(header);
        var ddsFileSize = (int)header.Length + logicalTexture.TotalSize;
        var pointer = new DisposablePtr(ddsFileSize);
        disposables.Add(pointer);
        using (var mem = new UnmanagedMemoryStream((byte*) pointer.value.ToPointer(), ddsFileSize, ddsFileSize, FileAccess.Write))
        {
            header.CopyTo(mem);
            logicalTexture.Data.CopyTo(mem);
        }

        var scratchImage = TexHelper.Instance.LoadFromDDSMemory(pointer.value, ddsFileSize, DDS_FLAGS.NONE);
        disposables.Add(scratchImage);
        var (_, compressed, _, _) = GetDxFormat(logicalTexture.Format, logicalTexture.Flags);
        if (compressed)
        {
            // block-compressed images can't be converted and have to be decompressed instead
            scratchImage = scratchImage.Decompress(0, DXGI_FORMAT.R8G8B8A8_UNORM);
            disposables.Add(scratchImage);
        }
        if (scratchImage.GetMetadata().Format != DXGI_FORMAT.R8G8B8A8_UNORM)
        {
            // maybe it was uncompressed, eg r8g8b8a8_unorm_srgb. convert it to regular colorspace with default options
            scratchImage = scratchImage.Convert(0, DXGI_FORMAT.R8G8B8A8_UNORM, TEX_FILTER_FLAGS.DEFAULT, 0.5f);
            disposables.Add(scratchImage);
        }
        var dxOutImage = scratchImage.GetImage(0);
        if (dxOutImage.RowPitch != logicalTexture.Size.Width*4)
        {
            log.LogWarning("DirectX unpacked texture [{name}] pitch {pitch} != width {width} * 4. Possible data corruption when converting to PNG", logicalTexture.Name, dxOutImage.RowPitch, logicalTexture.Size.Width);
        }

        var firstFrameByteSize = logicalTexture.Size.Width * logicalTexture.Size.Height * 4;
        var span = new Span<byte>(dxOutImage.Pixels.ToPointer(), firstFrameByteSize);
        return SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(span, logicalTexture.Size.Width, logicalTexture.Size.Height);
    }

    /// <summary>
    /// DDS header: 128 bytes for BC1/BC3, 148 bytes for SRGB variants and RGBA
    /// </summary>
    public async Task<Stream> BuildHeader(LogicalTexture logicalTexture, CancellationToken token)
    {
        var ms = new MemoryStream();
        await Utils.Write(ms, new byte[]{0x44, 0x44, 0x53, 0x20}, token); // "DDS " magic

        // DDS header
        var format = GetDxFormat(logicalTexture.Format, logicalTexture.Flags);
        var headerFlags = HeaderFlags.Required | HeaderFlags.MipmapCount;  // TODO mipmap level 1 needs special handling? maybe not
        //headerFlags |= logicalTexture.MipLevels > 1 ? HeaderFlags.MipmapCount : 0;
        headerFlags |= format.Compressed ? HeaderFlags.LinearSizeCompressed : HeaderFlags.PitchUncompressed;
        var caps = HeaderCaps.Mipmap | HeaderCaps.Texture | HeaderCaps.Complex;
        TexHelper.Instance.ComputePitch(format.DxFormat, logicalTexture.Size.Width, logicalTexture.Size.Height, out var rowPitch, out _, CP_FLAGS.NONE);
        var frameSize = logicalTexture.Size.Width * logicalTexture.Size.Height * 4 / format.CompressionRatio;
        await Utils.WriteUint4(ms, 0x7c, token);
        await Utils.WriteUint4(ms, (uint)headerFlags, token);
        await Utils.WriteUint4(ms, logicalTexture.Size.Height, token);
        await Utils.WriteUint4(ms, logicalTexture.Size.Width, token);
        await Utils.WriteUint4(ms, format.Compressed ? frameSize : rowPitch, token);
        await Utils.WriteUint4(ms, 1, token); // depth
        await Utils.WriteUint4(ms, logicalTexture.MipLevels, token);
        await Utils.WriteZeroes(ms, 11*4, token); // reserved
        await Utils.WriteUint4(ms, 0x20, token); // pixel format struct size
        await Utils.WriteUint4(ms, (uint)PixelFormatFlags.FourCC, token);
        if (format.Extended)
        {
            await Utils.Write(ms, new byte[]{0x44, 0x58, 0x31, 0x30}, token); // "DX10"
        }
        else
        {
            var fourcc = format.DxFormat == DXGI_FORMAT.BC1_UNORM ? new byte[] {0x44, 0x58, 0x54, 0x31} : new byte[] {0x44, 0x58, 0x54, 0x35}; // "DXT1" or "DXT5"
            await Utils.Write(ms, fourcc, token);
        }
        await Utils.WriteZeroes(ms, 5*4, token);
        await Utils.WriteUint4(ms, (uint)caps, token);
        await Utils.WriteZeroes(ms, 4*4, token);

        if (format.Extended)
        {
            // DXT10 extension header for srgb and rgba8 formats
            await Utils.WriteUint4(ms, (uint) format.DxFormat, token);
            await Utils.WriteUint4(ms, (uint) TEX_DIMENSION.TEXTURE2D, token);
            await Utils.WriteUint4(ms, 0, token);
            await Utils.WriteUint4(ms, 1, token);
            await Utils.WriteUint4(ms, 0, token);
        }

        ms.Position = 0;
        return ms;
    }

    private DxFormatInfo GetDxFormat(RfgCpeg.Entry.BitmapFormat format, TextureFlags flags) =>
        format switch
        {
            RfgCpeg.Entry.BitmapFormat.PcDxt1 => flags.HasFlag(TextureFlags.Srgb) ? new DxFormatInfo(DXGI_FORMAT.BC1_UNORM_SRGB, true, true, 8) : new DxFormatInfo(DXGI_FORMAT.BC1_UNORM, true, false, 8),
            RfgCpeg.Entry.BitmapFormat.PcDxt5 => flags.HasFlag(TextureFlags.Srgb) ? new DxFormatInfo(DXGI_FORMAT.BC3_UNORM_SRGB, true, true, 4) : new DxFormatInfo(DXGI_FORMAT.BC3_UNORM, true, false, 4),
            RfgCpeg.Entry.BitmapFormat.Pc8888 => flags.HasFlag(TextureFlags.Srgb) ? new DxFormatInfo(DXGI_FORMAT.R8G8B8A8_UNORM_SRGB, false, true, 1) : new DxFormatInfo(DXGI_FORMAT.R8G8B8A8_UNORM, false, true, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported texture format")
        };
}
/*
 TODO: colors are off when converted to PNG, especially in normal maps

*/
