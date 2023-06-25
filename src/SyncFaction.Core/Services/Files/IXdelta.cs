using PleOps.XdeltaSharp;

namespace SyncFaction.Core.Services.Files;

public interface IXdelta : IDisposable
{
    public event ProgressChangedHandler ProgressChanged;
    void Run();
}
