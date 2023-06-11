using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace SyncFaction.Toolbox.Args;

public class Get : Command
{
    private readonly Argument<FileInfo> archiveArg = new("archive", "vpp_pc to unpack");
    private readonly Argument<string> fileArg = new("file", "file inside vpp to extract");

    public Get() : base(nameof(Get).ToLowerInvariant(), "Extract certain file from vpp_pc")
    {
        AddArgument(archiveArg);
        AddArgument(fileArg);
        AddArgument(new Argument<FileInfo>("output", ParseOutFile, true, "output file (same as file, in archive's directory by default)"));
        AddOption(new Option<bool>(new[] {"-x", "--xml-format"}, "format xml file"));
        AddOption(new Option<bool>(new[] {"-f", "--force"}, "overwrite output if exists"));
        Handler = CommandHandler.Create(Handle);
    }

    private FileInfo ParseOutFile(ArgumentResult result)
    {
        var value = result.Tokens.SingleOrDefault()?.Value;

        if (value is not null && Path.IsPathFullyQualified(value))
        {
            return new FileInfo(value);
        }

        var dir = result.GetValueForArgument(archiveArg)?.Directory?.FullName;
        if (dir is null)
        {
            result.ErrorMessage = "Archive is not specified";
            return null!;
        }
        var fileName = value ?? result.GetValueForArgument(fileArg);
        return new FileInfo(Path.Combine(dir, fileName));
    }

    private async Task<int> Handle(FileInfo archive, string file, FileInfo output, bool xmlFormat, bool force, InvocationContext context, CancellationToken token)
    {
        var archiver = context.GetHost().Services.GetRequiredService<Commands>();
        await archiver.Get(archive, file, output, xmlFormat, force, token);
        return 0;
    }
}
