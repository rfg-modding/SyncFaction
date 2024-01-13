using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Models.Peg;
using SyncFaction.Packer.Services.Peg;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public class PegWalker
{
    private readonly IPegArchiver pegArchiver;
    private readonly ImageConverter imageConverter;
    private readonly ILogger<PegWalker> log;

    public PegWalker(IPegArchiver pegArchiver, ImageConverter imageConverter, ILogger<PegWalker> log)
    {
        this.pegArchiver = pegArchiver;
        this.imageConverter = imageConverter;
        this.log = log;
    }

    public async Task RepackPeg(FileInfo archive, DirectoryInfo edits, DirectoryInfo output, bool force, CancellationToken token)
    {
        var (cpu, gpu) = pegArchiver.GetPairFiles(archive);
        if (cpu is null || gpu is null)
        {
            throw new ArgumentException($"PEG does not have a pair: [{archive.FullName}]");
        }

        var name = Path.GetFileNameWithoutExtension(archive.Name);
        if (output.Exists)
        {
            if (output.EnumerateFileSystemInfos().Any() && !force)
            {
                throw new ArgumentException($"Output directory [{output.FullName}] is not empty. Use --force to overwrite");
            }

            output.Delete(true);
            output.Refresh();
        }

        output.Create();
        output.Refresh();

        await using var c = cpu.OpenRead();
        await using var g = gpu.OpenRead();
        var peg = await pegArchiver.UnpackPeg(c, g, name, token);
        var editFiles = edits
            .EnumerateFiles()
            .Where(x => x.Extension.ToLowerInvariant() == ".png")
            .ToDictionary(x => x.Name.ToLowerInvariant());
        var logicalTextures = WalkTextures(peg, editFiles, token).ToList();

        var cpuOut = new FileInfo(Path.Combine(output.FullName, $"{peg.Name}{Path.GetExtension(cpu.Name)}"));
        var gpuOut = new FileInfo(Path.Combine(output.FullName, $"{peg.Name}{Path.GetExtension(gpu.Name)}"));
        await using var cpuOutStream = cpuOut.OpenWrite();
        await using var gpuOutStream = gpuOut.OpenWrite();
        await pegArchiver.PackPeg(peg with {LogicalTextures = logicalTextures}, cpuOutStream, gpuOutStream, token);

    }

    private IEnumerable<LogicalTexture> WalkTextures(LogicalTextureArchive peg, Dictionary<string, FileInfo> editFiles, CancellationToken token)
    {
        foreach (var logicalTexture in peg.LogicalTextures)
        {
            if (editFiles.TryGetValue(logicalTexture.EditName, out var editFile))
            {
                editFiles.Remove(logicalTexture.EditName);
                var png = imageConverter.ReadPngFile(editFile, token).Result;
                log.LogInformation("Replacement for [{texture}]: [{file}] {w}x{h}", logicalTexture, editFile.FullName, png.Width, png.Height);
                if(logicalTexture.Flags.HasFlag(TextureFlags.HasAnimTiles) && (png.Width != logicalTexture.Size.Width || png.Height != logicalTexture.Size.Height))
                {
                    throw new InvalidOperationException($"Texture {logicalTexture.EditName} has animation tiles, can only replace with same-sized image. Expected {logicalTexture.Size}, got {png.Width}x{png.Height}");
                }

                var data = imageConverter.Encode(png, logicalTexture);
                yield return logicalTexture with {Data = data, Size = new Size(png.Width, png.Height), DataOffset = -1};
            }
            else
            {
                yield return logicalTexture;
            }
        }

        var newfiles = editFiles.OrderBy(x => x.Key, StringComparer.Ordinal);
        foreach (var (_, file) in newfiles)
        {
            var png = imageConverter.ReadPngFile(file, token).Result;
            var size = new Size(png.Width, png.Height);
            var (format, flags, mipLevels, name) = ParseFilename(file.Name);
            var stub = new LogicalTexture(size, size, new Size(0, 0), format, flags, mipLevels, -1, name, -1, -1, peg.Align, Stream.Null);
            log.LogInformation("New [{texture}] from [{file}]", stub, file.FullName);
            var data = imageConverter.Encode(png, stub);
            yield return stub with {Data = data};
        }


        /*
         TODO invent sane name format for new textures
         * no order number (order by filename)
         * format and flag: dxt1, dxt5, rgba, dxt1_srgb, dxt5_srgb, rgba_srgb
         * mip levels

         "foo_bar rgba_srgb 5.png"

        */
    }

    private (RfgCpeg.Entry.BitmapFormat format, TextureFlags flags, int mipLevels, string name) ParseFilename(string fileName)
    {
        var match = nameFormat.Match(fileName.ToLowerInvariant());
        var name = match.Groups["name"].Value + ".tga";
        var formatString = match.Groups["format"].Value;
        var mipLevels = int.Parse(match.Groups["mipLevels"].Value);

        var (format, flags) = formatString switch
        {
            "dxt1" => (RfgCpeg.Entry.BitmapFormat.PcDxt1, TextureFlags.None),
            "dxt1_srgb" => (RfgCpeg.Entry.BitmapFormat.PcDxt1, TextureFlags.Srgb),
            "dxt5" => (RfgCpeg.Entry.BitmapFormat.PcDxt5, TextureFlags.None),
            "dxt5_srgb" => (RfgCpeg.Entry.BitmapFormat.PcDxt5, TextureFlags.Srgb),
            "rgba" => (RfgCpeg.Entry.BitmapFormat.Pc8888, TextureFlags.None),
            "rgba_srgb" => (RfgCpeg.Entry.BitmapFormat.Pc8888, TextureFlags.Srgb),
            _ => throw new ArgumentOutOfRangeException(nameof(formatString), formatString, "Unknown texture format from filename")
        };

        return (format, flags, mipLevels, name);
    }

    private readonly Regex nameFormat = new (@"^(?<name>.*?)\s+(?<format>.*?)\s+(?<mipLevels>\d+).png$");
}
