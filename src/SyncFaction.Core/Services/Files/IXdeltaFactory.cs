namespace SyncFaction.Core.Services.Files;

public interface IXdeltaFactory
{
    IXdelta Create(Stream srcStream, Stream patchStream, Stream dstStream);
}
