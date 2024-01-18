using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using NLog.Fluent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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

    public Unpeg() : base(nameof(Unpeg).ToLowerInvariant(), "TEST TODO: debug compare dds and png")
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

            await Write(logicalTexture, converter, log, token);

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

    private async Task Write(LogicalTexture logicalTexture, ImageConverter imageConverter, ILogger<Unpeg> log, CancellationToken token)
    {
        var name = Path.GetFileNameWithoutExtension(logicalTexture.Name);
        log.LogInformation("=== {name} ===", name);
        var pngImage1 = await WriteAndGetPng(logicalTexture, name, imageConverter, log, token);

        var roundtripRaw = imageConverter.Encode(pngImage1, logicalTexture);
        if (roundtripRaw.Position != 0)
        {
            throw new InvalidOperationException($"rewind! {logicalTexture}");
        }
        var roundtripTexture = logicalTexture with {Data = roundtripRaw};

        var pngImage2 = await WriteAndGetPng(roundtripTexture, $"{name}_roundtrip", imageConverter, log, token);
        if (roundtripTexture.Data.Length != logicalTexture.Data.Length)
        {
            var diff = roundtripTexture.Data.Length - logicalTexture.Data.Length;
            if (diff != 8)
            {
                throw new InvalidOperationException($"Roundtrip data size is : {roundtripRaw.Length} != {logicalTexture.Data.Length}. {logicalTexture}");
            }
            else
            {
                log.LogWarning($"{name} roundtrip size diff = 8 (new is bigger)", name);
            }
        }
        var result1 = Compare(pngImage1, pngImage2);
        log.LogInformation("{name} compare result: {result}", name, result1);

        return;
        var roundtripRaw2 = imageConverter.Encode(pngImage2, roundtripTexture);
        var roundtripTexture2 = logicalTexture with {Data = roundtripRaw2};
        var pngImage3 = await WriteAndGetPng(roundtripTexture2, $"{name}_roundtrip2", imageConverter, log, token);
        var result2 = Compare(pngImage2, pngImage3);
        log.LogInformation("{name} compare result: {result}", name, result2);
    }

    private async Task<Image<Rgba32>> WriteAndGetPng(LogicalTexture logicalTexture, string name, ImageConverter imageConverter, ILogger<Unpeg> log, CancellationToken token)
    {
        var raw = new FileInfo($"_{name}.raw");
        log.LogDebug("Writing {name} {size}", raw.Name, logicalTexture.Data.Length);
        /*await using var s = raw.OpenWrite();
        await logicalTexture.Data.CopyToAsync(s, token);*/
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);

        var dds = new FileInfo($"_{name}.dds");
        await using var d = dds.OpenWrite();
        var header = await imageConverter.BuildHeader(logicalTexture, token);
        /*await header.CopyToAsync(d, token);
        await logicalTexture.Data.CopyToAsync(d, token);*/
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);

        var png = new FileInfo($"_{name}.png");
        var pngImage = imageConverter.DecodeFirstFrame(logicalTexture);
        /*await using var pngOut = png.OpenWrite();
        await imageConverter.WritePngFile(pngImage, pngOut, token);*/
        return pngImage;
    }

    private CompareResult Compare(Image<Rgba32> image, Image<Rgba32> other)
    {
        if (image.Width != other.Width || image.Height != other.Height)
        {
            throw new ArgumentException("Image sizes are different");
        }

        var quantity = image.Width * image.Height;
        var absoluteError = 0;
        var pixelErrorCount = 0;

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var actualPixel = image[x, y];
                var expectedPixel = other[x, y];

                var r = Math.Abs(expectedPixel.R - actualPixel.R);
                var g = Math.Abs(expectedPixel.G - actualPixel.G);
                var b = Math.Abs(expectedPixel.B - actualPixel.B);
                var a = Math.Abs(expectedPixel.A - actualPixel.A);
                absoluteError = absoluteError + r + g + b + a;

                pixelErrorCount += r + g + b + a > 0 ? 1 : 0;
            }
        }
        var meanError = (double)absoluteError / quantity;
        var pixelErrorPercentage = (double)pixelErrorCount / quantity * 100;
        return new CompareResult(absoluteError, meanError, pixelErrorCount, pixelErrorPercentage);
    }

    /// <param name="MeanError">Mean pixel error of absolute pixel error</param>
    /// <param name="AbsoluteError">Absolute error, counts each color channel on every pixel the delta</param>
    /// <param name="PixelErrorCount">Number of pixels that differ between images</param>
    /// <param name="PixelErrorPercentage">Percentage of pixels that differ between images</param>
    public record CompareResult(int AbsoluteError, double MeanError, int PixelErrorCount, double PixelErrorPercentage);
}
