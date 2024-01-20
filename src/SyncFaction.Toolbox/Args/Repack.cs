using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox.Args;

public class Repack : Command
{
    private readonly Argument<string> archiveArg = new("archive", "vpp or peg archive to repack, globs allowed");
    private readonly Argument<string> replacementsArg = new("replacements", "path with files to replace or add into archive, recursive sub-archives supported");
    private readonly Argument<string> outputArg = new("output", () => Constatns.DefaultDir, "output path");

    private readonly Option<bool> force = new(new[]
        {
            "-f",
            "--force"
        },
        "overwrite output if exists");

    public override string? Description => @"Apply replacements or add files to archives and containers
Supported formats: " + string.Join(" ", Constatns.KnownArchiveExtensions.Concat(Constatns.KnownTextureArchiveExtensions));

    public Repack() : base(nameof(Repack).ToLowerInvariant())
    {

        AddArgument(archiveArg);
        AddArgument(replacementsArg);
        AddArgument(outputArg);
        AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(InvocationContext context, CancellationToken token)
    {
        var archive = context.ParseResult.GetValueForArgument(archiveArg);
        var replacements = context.ParseResult.GetValueForArgument(replacementsArg);
        var output = context.ParseResult.GetValueForArgument(outputArg);
        var force = context.ParseResult.GetValueForOption(this.force);
        var settings = new RepackSettings(archive, replacements, output, force);
        var archiver = context.GetHost().Services.GetRequiredService<Archiver>();
        //await archiver.Repack(settings, token);
        throw new NotImplementedException();
        return 0;
    }
}
