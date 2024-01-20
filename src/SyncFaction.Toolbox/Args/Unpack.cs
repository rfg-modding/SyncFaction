using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox.Args;

public class Unpack : Command
{
    private readonly Argument<string> archiveArg = new("archive", "vpp or peg archive to unpack, globs allowed");
    private readonly Argument<string> fileArg = new("file", () => "*", "file inside archive to extract, globs allowed. lookup is not recursive!");
    private readonly Argument<string> outputArg = new("output", () => Constatns.DefaultDir, "output path");

    private readonly Option<bool> xmlFormat = new(new[]
        {
            "-x",
            "--xml-format"
        },
        "format xml-like files (.xtbl .dtdox .gtdox) for readability, some files will become unusable in game");

    private readonly Option<bool> recursive = new(new[]
        {
            "-r",
            "--recursive"
        },
        $"unpack nested archives (typically .str2_pc) recursively in {Constatns.DefaultDir} subfolder");

    private readonly Option<List<TextureFormat>> textures = new(new[]
        {
            "-t",
            "--textures"
        },
        $"unpack textures from containers (.cpeg_pc .cvbm_pc .gpeg_pc .gvbm_pc) in {Constatns.DefaultDir} subfolder. Specify one or more supported formats: dds png raw")
    {
        ArgumentHelpName = "formats",
        AllowMultipleArgumentsPerToken = true,
    };

    private readonly Option<bool> metadata = new(new[]
        {
            "-m",
            "--metadata"
        },
        $"write {Constatns.MetadataFile} file with archive information: entries, sizes, hashes");

    private readonly Option<bool> force = new(new[]
        {
            "-f",
            "--force"
        },
        "overwrite output if exists");

    private readonly Option<int> parallel = new(new[]
        {
            "-p",
            "--parallel"
        },
        "number of parallel tasks. Defaults to processor core count. Use 1 for lower RAM usage")
    {
        ArgumentHelpName = "N"
    };

    public override string? Description => @"Extract archives and containers
Supported formats: " + string.Join(" ", Constatns.KnownArchiveExtensions.Concat(Constatns.KnownTextureArchiveExtensions));

    public Unpack() : base(nameof(Unpack).ToLowerInvariant())
    {

        AddArgument(archiveArg);
        AddArgument(fileArg);
        AddArgument(outputArg);
        AddOption(xmlFormat);
        AddOption(recursive);
        AddOption(textures);
        AddOption(metadata);
        AddOption(force);
        AddOption(parallel);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(InvocationContext context, CancellationToken token)
    {
        var archive = context.ParseResult.GetValueForArgument(archiveArg);
        var file = context.ParseResult.GetValueForArgument(fileArg);
        var output = context.ParseResult.GetValueForArgument(outputArg);
        var xmlFormat = context.ParseResult.GetValueForOption(this.xmlFormat);
        var recursive = context.ParseResult.GetValueForOption(this.recursive);
        var textures = context.ParseResult.GetValueForOption(this.textures) ?? new List<TextureFormat>();
        var metadata = context.ParseResult.GetValueForOption(this.metadata);
        var force = context.ParseResult.GetValueForOption(this.force);
        var parallel = context.ParseResult.GetValueForOption(this.parallel);
        var settings = new UnpackSettings(archive, file, output, xmlFormat, recursive, textures, metadata, force, parallel < 1 ? Environment.ProcessorCount : parallel);
        var archiver = context.GetHost().Services.GetRequiredService<Archiver>();
        await archiver.Unpack(settings, token);
        return 0;
    }
}
