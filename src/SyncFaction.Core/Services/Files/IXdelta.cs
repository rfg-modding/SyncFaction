using System.Diagnostics.CodeAnalysis;
using PleOps.XdeltaSharp;

namespace SyncFaction.Core.Services.Files;

[SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "What?")]
public interface IXdelta : IDisposable
{
    public event ProgressChangedHandler ProgressChanged;
    void Run();
}
