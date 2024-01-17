using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox.Args;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
public class Get : Command
{
    private readonly Argument<string> archiveArg = new("archive", "vpp_pc to unpack, globs allowed");
    private readonly Argument<string> fileArg = new("file", "file inside vpp to extract, globs allowed");
    private readonly Argument<string> outputArg = new("output", () => Archiver.DefaultDir, "output path");

    private readonly Option<bool> xmlFormat = new(new[]
        {
            "-x",
            "--xml-format"
        },
        "format xml file");

    private readonly Option<bool> force = new(new[]
        {
            "-f",
            "--force"
        },
        "overwrite output if exists");

    public Get() : base(nameof(Get).ToLowerInvariant(), "Extract certain file from vpp_pc to dir")
    {
        AddArgument(archiveArg);
        AddArgument(fileArg);
        AddArgument(outputArg);
        AddOption(xmlFormat);
        AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(string archive, string file, string output, bool xmlFormat, bool force, InvocationContext context, CancellationToken token)
    {
        var settings = new UnpackSettings(archive, file, output, xmlFormat, false, new List<Archiver.TextureFormat>(), false, force, 1);
        var archiver = context.GetHost().Services.GetRequiredService<Archiver>();
        await archiver.Unpack(settings, token);
        return 0;
    }
}
