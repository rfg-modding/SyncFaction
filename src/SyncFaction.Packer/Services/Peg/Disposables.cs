namespace SyncFaction.Packer.Services.Peg;

class Disposables : List<IDisposable>, IDisposable
{
    public void Dispose()
    {
        Reverse();
        foreach (var item in this)
        {
            item.Dispose();
        }
    }
}