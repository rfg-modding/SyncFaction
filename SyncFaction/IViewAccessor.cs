using System.Windows.Controls;

namespace SyncFaction;

public interface IViewAccessor
{
    public ListView OnlineModListView { get; }
}