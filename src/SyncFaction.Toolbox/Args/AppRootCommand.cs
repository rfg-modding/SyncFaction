using System.CommandLine;
using System.CommandLine.Invocation;

namespace SyncFaction.Toolbox.Args;

public class AppRootCommand : RootCommand
{
    public AppRootCommand()
    {
        AddCommand(new Unpack());
        AddCommand(new Unpeg());
        AddCommand(new Repeg());
        //AddCommand(new Pack());
    }

    public class CommandHandler : ICommandHandler
    {
        public int Invoke(InvocationContext context) => throw new NotImplementedException();

        public async Task<int> InvokeAsync(InvocationContext context) => 0;
    }
}
