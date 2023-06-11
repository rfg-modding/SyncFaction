using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace SyncFaction.Toolbox.Args;

public class Unpack : Command
{
    private readonly Argument<FileInfo> archiveArg = new("archive", "vpp_pc to unpack");

    public Unpack() : base(nameof(Unpack).ToLowerInvariant(), "Extract vpp_pc")
    {
        AddArgument(archiveArg);
        AddArgument(new Argument<DirectoryInfo>("dir", ParseDir, true, "output path (archive's directory by default)"));
        AddOption(new Option<bool>(new[] {"-f", "--force"}, "overwrite output if exists"));
        //AddOption(new Option<bool>(new[] {"-r", "--recursive"}, "unpack str2 files too")); TODO
        ////AddOption(new Option<bool>(new[] {"-m", "--metadata"}, "write _metadata.json file with archive information")); TODO
        Handler = CommandHandler.Create(Handle);
    }

    private DirectoryInfo ParseDir(ArgumentResult result)
    {
        var value = result.Tokens.SingleOrDefault()?.Value;
        var path = value ?? result.GetValueForArgument(archiveArg)?.Directory?.FullName;
        if (path is null)
        {
            result.ErrorMessage = "Archive is not specified";
            return null!;
        }
        return new DirectoryInfo(path);
    }

    private async Task<int> Handle(FileInfo archive, DirectoryInfo dir, bool force, InvocationContext context, CancellationToken token)
    {
        var archiver = context.GetHost().Services.GetRequiredService<Commands>();
        await archiver.Unpack(archive, dir, force, token);
        return 0;
    }
}
