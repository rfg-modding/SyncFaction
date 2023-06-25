using PleOps.XdeltaSharp;
using PleOps.XdeltaSharp.Decoder;

namespace SyncFaction.Core.Services.Files;

public class XdeltaWrapper : IXdelta
{
    private readonly Decoder decoder;

    public event ProgressChangedHandler? ProgressChanged
    {
        add => decoder.ProgressChanged += value;
        remove => decoder.ProgressChanged -= value;
    }

    public XdeltaWrapper(Stream srcStream, Stream patchStream, Stream dstStream) => decoder = new Decoder(srcStream, patchStream, dstStream);

    public void Dispose() => decoder.Dispose();

    public void Run() => decoder.Run();
}
