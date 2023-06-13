using System.CommandLine;
using System.CommandLine.Invocation;

namespace SyncFaction.Toolbox.Args;

public class AppRootCommand : RootCommand
{
    public AppRootCommand()
    {
        AddCommand(new Unpack());
        AddCommand(new Pack());
        AddCommand(new Get());
    }

    public class CommandHandler : ICommandHandler
    {

        public int Invoke(InvocationContext context)
        {
            throw new NotImplementedException();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            return 0;
        }
    }

}