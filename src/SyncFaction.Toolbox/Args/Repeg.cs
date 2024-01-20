using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox.Args;

public class Repeg : Command
{
    private readonly Argument<string> pegArg = new("peg", "One peg filename: foo.cpeg_pc, foo.gpeg_pc, foo.cvbm_pc, foo.gvbm_pc");
    private readonly Argument<string> editsArg = new("edits", "Folder with replacement and new textures");
    private readonly Argument<string> outputArg = new("output", () => Constatns.DefaultOutputDir, "output path");

    private readonly Option<bool> force = new(new[]
        {
            "-f",
            "--force"
        },
        "overwrite output if exists");

    public Repeg() : base(nameof(Repeg).ToLowerInvariant(), "Repack peg with replacement or new textures")
    {
        AddArgument(pegArg);
        AddArgument(editsArg);
        AddArgument(outputArg);
        AddOption(force);
        Handler = CommandHandler.Create(Handle);
    }

    private async Task<int> Handle(string peg, string edits, string output, bool force, InvocationContext context, CancellationToken token)
    {
        var pegFile = new FileInfo(peg);
        if (!pegFile.Exists)
        {
            throw new ArgumentException($"Peg file does not exist: [{pegFile.FullName}]");
        }

        var editsDir = new DirectoryInfo(edits);
        if (!editsDir.Exists)
        {
            throw new ArgumentException($"Edits directory does not exist: [{editsDir.FullName}]");
        }

        var outputDir = new DirectoryInfo(output);

        var walker = context.GetHost().Services.GetRequiredService<PegWalker>();
        await walker.RepackPeg(pegFile, editsDir, outputDir, force, token);
        return 0;
    }
}
