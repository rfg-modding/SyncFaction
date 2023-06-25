using System.Diagnostics.CodeAnalysis;
using PleOps.XdeltaSharp;
using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This class does not own disposable objects")]
public sealed class FakeXdeltaConcat : IXdelta
{
    private readonly Stream srcStream;
    private readonly Stream patchStream;
    private readonly Stream dstStream;

    public event ProgressChangedHandler ProgressChanged;

    public FakeXdeltaConcat(Stream srcStream, Stream patchStream, Stream dstStream)
    {
        this.srcStream = srcStream;
        this.patchStream = patchStream;
        this.dstStream = dstStream;
    }

    public void Dispose() { }

    public void Run()
    {
        srcStream.CopyTo(dstStream);
        patchStream.CopyTo(dstStream);
        ProgressChanged?.Invoke(1);
    }
}
