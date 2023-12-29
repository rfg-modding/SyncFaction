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
        var editFiles = edits.EnumerateFiles().ToDictionary(x => x.Name.ToLowerInvariant());
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
        /*
         TODO invent sane name format for new textures
         * no order number (order by filename)
         * format and flag: dxt1, dxt5, rgba, dxt1_srgb, dxt5_srgb, rgba_srgb
         * mip levels

         "foo_bar rgba_srgb 5.png"

        */
    }
}
