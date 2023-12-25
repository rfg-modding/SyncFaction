using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Packer.Models.Peg;
using SyncFaction.Packer.Services.Peg;

namespace SyncFaction.Toolbox.Args;

public class Unpeg : Command
{
    private readonly Argument<string> nameArg = new("name", "peg name to unpack, two files are read: cpeg_pc and gpeg_pc");
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

    public Unpeg() : base(nameof(Unpeg).ToLowerInvariant(), "Extract peg to dir")
    {
        AddArgument(nameArg);
        AddArgument(outputArg);
        AddOption(metadata);
        AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(string name, string output, bool xmlFormat, bool recursive, bool metadata, bool force, InvocationContext context, CancellationToken token)
    {
        var path = Path.GetDirectoryName(name);
        var nameNoExt = Path.GetFileNameWithoutExtension(name);
        //var settings = new UnpackSettings(nameNoExt, "*", output, xmlFormat, recursive, metadata, force);
        var archiver = context.GetHost().Services.GetRequiredService<PegArchiver>();
        Console.WriteLine(name);
        Console.WriteLine(path);
        Console.WriteLine(nameNoExt);
        var cpu = new FileInfo(Path.Combine(path, nameNoExt + ".cpeg_pc")).OpenRead();
        var gpu = new FileInfo(Path.Combine(path, nameNoExt + ".gpeg_pc")).OpenRead();
        var logicalTextureArchive = await archiver.UnpackPeg(cpu, gpu, nameNoExt, token);
        Console.WriteLine(JsonSerializer.Serialize(logicalTextureArchive, new JsonSerializerOptions(){WriteIndented = true}));

        var outputDir = new DirectoryInfo(output);
        if (!outputDir.Exists)
        {
            outputDir.Create();
            outputDir.Refresh();
        }

        var tasks = new List<Task>();
        var converter = new ImageConverter();

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

        return 0;
    }

    private async Task Write(LogicalTexture logicalTexture, FileInfo outputFile, ImageConverter imageConverter, CancellationToken token)
    {
        //return;
        var rawFile = new FileInfo(outputFile.FullName + ".raw");
        await using var s = rawFile.OpenWrite();
        await logicalTexture.Data.CopyToAsync(s, token);
        logicalTexture.Data.Seek(0, SeekOrigin.Begin);
        var image = imageConverter.Decode(logicalTexture);
        await using var pngOut = outputFile.OpenWrite();
        await imageConverter.WritePngFile(image, pngOut, token);
    }

}
