using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox.Args;

public class Unpack : Command
{
    private readonly Argument<string> archiveArg = new("archive", "vpp_pc to unpack, globs allowed");
    private readonly Argument<string> outputArg = new("output", () => Archiver.DefaultDir, "output path");
    private readonly Option<bool> xmlFormat = new(new[] {"-x", "--xml-format"}, "format xml file");
    private readonly Option<bool> recursive = new(new[] {"-r", "--recursive"}, $"unpack nested archives recursively in default subfolder ({Archiver.DefaultDir})");
    private readonly Option<bool> metadata = new(new[] {"-m", "--metadata"}, $"write file with archive information ({Archiver.MetadataFile})");
    private readonly Option<bool> force = new(new[] {"-f", "--force"}, "overwrite output if exists");

    public Unpack() : base(nameof(Unpack).ToLowerInvariant(), "Extract vpp_pc to dir")
    {
        AddArgument(archiveArg);
        AddArgument(outputArg);
        AddOption(xmlFormat);
        AddOption(recursive);
        AddOption(metadata);
        AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(string archive, string output, bool xmlFormat, bool recursive, bool metadata, bool force, InvocationContext context, CancellationToken token)
    {
        var settings = new UnpackSettings(archive, "*", output, xmlFormat, recursive, metadata, force);
        var archiver = context.GetHost().Services.GetRequiredService<Archiver>();
        await archiver.Unpack(settings, token);
        return 0;
    }
}
