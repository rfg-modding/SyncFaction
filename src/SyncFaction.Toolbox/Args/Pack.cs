using System.CommandLine;
using System.CommandLine.Invocation;

namespace SyncFaction.Toolbox.Args;

public class Pack : Command
{
    public Pack() : base(nameof(Pack).ToLowerInvariant(), "Compress vpp_pc")
    {
        AddArgument(new Argument<DirectoryInfo>("dir", "path to compress"));
        AddArgument(new Argument<FileInfo>("archive", "vpp_pc output"));
        AddOption(new Option<bool>(new[]
            {
                "f",
                "force"
            },
            "overwrite output if exists"));
    }

    public class CommandHandler : ICommandHandler
    {
        public int Invoke(InvocationContext context) => throw new NotImplementedException();

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            throw new NotImplementedException();

            return 0;
        }
    }
}
