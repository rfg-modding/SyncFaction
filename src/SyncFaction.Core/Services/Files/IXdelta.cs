using PleOps.XdeltaSharp;

namespace SyncFaction.Core.Services.Files;

public interface IXdelta : IDisposable
{
    void Run();
    public event ProgressChangedHandler ProgressChanged;
}