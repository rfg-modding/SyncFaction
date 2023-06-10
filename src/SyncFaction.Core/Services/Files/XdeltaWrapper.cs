using PleOps.XdeltaSharp;
using PleOps.XdeltaSharp.Decoder;

namespace SyncFaction.Core.Services.Files;

public class XdeltaWrapper : IXdelta
{
    public XdeltaWrapper(Stream srcStream, Stream patchStream, Stream dstStream)
    {
        decoder = new Decoder(srcStream, patchStream, dstStream);
    }

    private readonly Decoder decoder;

    public void Dispose()
    {
        decoder.Dispose();
    }

    public void Run()
    {
        decoder.Run();
    }

    public event ProgressChangedHandler? ProgressChanged
    {
        add => decoder.ProgressChanged += value;
        remove => decoder.ProgressChanged -= value;
    }
}
