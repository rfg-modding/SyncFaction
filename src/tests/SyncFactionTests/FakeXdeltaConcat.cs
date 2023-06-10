using PleOps.XdeltaSharp;
using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

public class FakeXdeltaConcat : IXdelta
{
    private readonly Stream srcStream;
    private readonly Stream patchStream;
    private readonly Stream dstStream;
    public bool disposed;

    public FakeXdeltaConcat(Stream srcStream, Stream patchStream, Stream dstStream)
    {
        this.srcStream = srcStream;
        this.patchStream = patchStream;
        this.dstStream = dstStream;
    }

    public void Dispose()
    {
        disposed = true;
    }

    public void Run()
    {
        srcStream.CopyTo(dstStream);
        patchStream.CopyTo(dstStream);
        ProgressChanged?.Invoke(1);
    }

    public event ProgressChangedHandler? ProgressChanged;
}
