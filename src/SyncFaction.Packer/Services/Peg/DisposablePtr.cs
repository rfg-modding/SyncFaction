using System.Runtime.InteropServices;

namespace SyncFaction.Packer.Services.Peg;

class DisposablePtr : IDisposable
{
    public DisposablePtr(int length)
    {
        value = Marshal.AllocHGlobal(length);
    }

    public readonly IntPtr value;

    public void Dispose()
    {
        Marshal.FreeHGlobal(value);
    }
}