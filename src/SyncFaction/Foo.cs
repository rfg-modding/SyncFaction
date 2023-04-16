using System.Collections.ObjectModel;

namespace SyncFaction;

public class Foo
{
    public ObservableCollection<string> Values { get; } = new();
}