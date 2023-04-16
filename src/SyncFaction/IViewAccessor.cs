using System.Windows;
using System.Windows.Controls;

namespace SyncFaction;

public interface IViewAccessor
{
    public ListView OnlineModListView { get; }
    public ListView LocalModListView { get; }
    public Window WindowView { get; }
}