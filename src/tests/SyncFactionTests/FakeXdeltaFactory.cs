using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

public class FakeXdeltaFactory : IXdeltaFactory
{
    public FakeXdeltaConcat instance = null;

    public IXdelta Create(Stream srcStream, Stream patchStream, Stream dstStream)
    {
        if (instance != null)
        {
            throw new InvalidOperationException();
        }
        instance = new FakeXdeltaConcat(srcStream, patchStream, dstStream);
        return instance;
    }
}
