using System.Diagnostics.CodeAnalysis;
using SyncFaction.Core.Services.Files;

namespace SyncFactionTests;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "This class does not own disposable objects")]
public class FakeXdeltaFactory : IXdeltaFactory
{
    private FakeXdeltaConcat instance;

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
