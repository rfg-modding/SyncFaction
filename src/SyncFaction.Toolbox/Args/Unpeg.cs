using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using NLog.Fluent;
using SixLabors.ImageSharp.Formats.Png;
using SyncFaction.Packer.Models.Peg;
using SyncFaction.Packer.Services.Peg;

namespace SyncFaction.Toolbox.Args;

public class Unpeg : Command
{
    private readonly Argument<string> pathArg = new("path", "path to look for pegs");
    private readonly Argument<string> outputArg = new("output", () => Archiver.DefaultDir, "output path");

    private readonly Option<bool> metadata = new(new[]
        {
            "-m",
            "--metadata"
        },
        $"write file with archive information ({Archiver.MetadataFile})");

    private readonly Option<bool> force = new(new[]
        {
            "-f",
            "--force"
        },
        "overwrite output if exists");

    public Unpeg() : base(nameof(Unpeg).ToLowerInvariant(), "Read peg info")
    {
        AddArgument(pathArg);
        //AddArgument(outputArg);
        //AddOption(metadata);
        //AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(string path, InvocationContext context, CancellationToken token)
    {
        var absPath = new DirectoryInfo(path).FullName;
        Console.WriteLine(absPath);
        var services = context.GetHost().Services;
        var log = services.GetRequiredService<ILogger<Unpeg>>();
        var archiver = services.GetRequiredService<IPegArchiver>();

        var converter = new ImageConverter(services.GetRequiredService<ILogger<ImageConverter>>());
        var documentMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        documentMatcher.AddInclude("**/*.cvbm_pc");
        documentMatcher.AddInclude("**/*.cpeg_pc");
        var paths = documentMatcher.GetResultsInFullPath(absPath).ToList();
        foreach (var filePath in paths)
        {
            token.ThrowIfCancellationRequested();
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                // skip dirs
                continue;
            }

            await GetStats(filePath, archiver, converter, log, token);

        }


        return 0;

        /*
        var outputDir = new DirectoryInfo(null);
        if (!outputDir.Exists)
        {
            outputDir.Create();
            outputDir.Refresh();
        }

        var tasks = new List<Task>();
        foreach (var logicalTexture in logicalTextureArchive.LogicalTextures)
        {
            var newName = Path.ChangeExtension(logicalTexture.Name, ".png");
            var outputFile = new FileInfo(Path.Combine(outputDir.FullName, newName));
            if (outputFile.Exists)
            {
                throw new InvalidOperationException($"File [{outputFile.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }
            tasks.Add(Write(logicalTexture, outputFile, converter, token));
        }

        await Task.WhenAll(tasks);

        var outputCpu = new FileInfo(Path.Combine(outputDir.FullName, "test.cpeg_pc"));
        var outputGpu = new FileInfo(Path.Combine(outputDir.FullName, "test.gpeg_pc"));
        using var writer = new PegWriter(logicalTextureArchive);
        await using var c = outputCpu.OpenWrite();
        await using var g = outputGpu.OpenWrite();
        await writer.WriteAll(c, g, token);

        return 0;*/
    }

    private async Task GetStats(string filePath, IPegArchiver archiver, ImageConverter converter, ILogger<Unpeg> log, CancellationToken token)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);

        var (cpuFile, gpuFile) = archiver.GetPairFiles(new FileInfo(filePath));
        if (cpuFile is null || gpuFile is null)
        {
            throw new InvalidOperationException("Can not find pair cpu+gpu files");
        }
        log.LogDebug("cpu {cpu}, gpu {gpu}", cpuFile.FullName, gpuFile.FullName);

        await using var cpu = cpuFile.OpenRead();
        await using var gpu = gpuFile.OpenRead();
        var logicalTextureArchive = await archiver.UnpackPeg(cpu, gpu, nameNoExt, token);


        foreach (var logicalTexture in logicalTextureArchive.LogicalTextures)
        {
            token.ThrowIfCancellationRequested();
            log.LogInformation("{file} {texture}", cpuFile.FullName, logicalTexture);

            if (logicalTexture.Name.Contains("armband_n.tga"))
            {
                await Write(logicalTexture, new FileInfo($"_{logicalTexture.Name}.png"), converter, token);
                return;
            }

            //var png = converter.Decode(logicalTexture);
            //var result = converter.Encode(png, logicalTexture);
            //var pngResult = converter.Decode(logicalTexture with {Data = result});

            //var encoder = new PngEncoder();

            //using var raw = File.OpenWrite($"{logicalTexture.Name}.1.raw");
            //logicalTexture.Data.Position = 0;
            //logicalTexture.Data.CopyTo(raw);

            //using var png1 = File.OpenWrite($"_{logicalTexture.Name}.1.png");
            //await png.SaveAsync(png1, encoder, token);

            /*using var conv = File.OpenWrite($"{logicalTexture.Name}.2conv");
            result.Position = 0;
            result.CopyTo(conv);

            using var png2 = File.OpenWrite($"{logicalTexture.Name}.2.png");
            await pngResult.SaveAsync(png2, encoder, token);*/

        }

    }

    private async Task Write(LogicalTexture logicalTexture, FileInfo outputFile, ImageConverter imageConverter, CancellationToken token)
    {
        var rawFile = new FileInfo(outputFile.FullName + ".raw");
        await using var s = rawFile.OpenWrite();
        await logicalTexture.Data.CopyToAsync(s, token);
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);

        var ddsFile = new FileInfo(outputFile.FullName + ".raw.dds");
        await using var d = ddsFile.OpenWrite();
        var header = await imageConverter.BuildHeader(logicalTexture, token);
        await header.CopyToAsync(d, token);
        await logicalTexture.Data.CopyToAsync(d, token);
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);

        var image = imageConverter.DecodeFirstFrame(logicalTexture);
        await using var pngOut = outputFile.OpenWrite();
        await imageConverter.WritePngFile(image, pngOut, token);
    }

}
