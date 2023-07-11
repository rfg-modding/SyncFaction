namespace SyncFaction.Core.Services.Files;

public class XdeltaFactory : IXdeltaFactory
{
    public IXdelta Create(Stream srcStream, Stream patchStream, Stream dstStream) => new XdeltaWrapper(srcStream, patchStream, dstStream);
}
