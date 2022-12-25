using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using GongSolutions.Wpf.DragDrop;

namespace SyncFaction;

/// <summary>
/// Adopted from drag-drop library samples
/// </summary>
public class GroupedDropHandler : IDropTarget
{

    /// <inheritdoc />
    public void DragOver(IDropInfo dropInfo)
    {
        GongSolutions.Wpf.DragDrop.DragDrop.DefaultDropHandler.DragOver(dropInfo);
        if (dropInfo.TargetGroup == null)
        {
            dropInfo.Effects = DragDropEffects.None;
        }

        // higihlght target group
        var listView = (ListView) dropInfo.VisualTarget;
        foreach (var visualGroup in FindVisualChildren<GroupItem>(listView))
        {
            var grid = FindVisualChildren<Grid>(visualGroup).First();
            if (dropInfo.TargetGroup != dropInfo.DragInfo.SourceGroup && dropInfo.TargetGroup == visualGroup.DataContext)
            {
                // highlight only target group if target is not source group
                grid.Background = ViewModel.Highlight;
            }
            else
            {
                // paint other groups back if they were affected before
                grid.Background = Brushes.Transparent;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject?
    {
        if (depObj == null)
        {
            yield break;
        }
        var count = VisualTreeHelper.GetChildrenCount(depObj);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T result)
            {
                yield return result;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    /// <inheritdoc />
    public void Drop(IDropInfo dropInfo)
    {
        // The default drop handler don't know how to set an item's group. You need to explicitly set the group on the dropped item like this.
        GongSolutions.Wpf.DragDrop.DragDrop.DefaultDropHandler.Drop(dropInfo);

        // Now extract the dragged group items and set the new group (target)
        var data = DefaultDropHandler.ExtractData(dropInfo.Data).OfType<LocalModViewModel>().ToList();
        if (dropInfo.TargetGroup.Name is not LocalModStatus)
        {
            throw new ArgumentOutOfRangeException($"group name type is [{dropInfo.TargetGroup.Name?.GetType()}]");
        }

        var group = (LocalModStatus)dropInfo.TargetGroup.Name;
        foreach (var groupedItem in data)
        {
            groupedItem.Status = group;
        }

        // Changing group data at runtime isn't handled well: force a refresh on the collection view.
        if (dropInfo.TargetCollection is ICollectionView)
        {
            ((ICollectionView)dropInfo.TargetCollection).Refresh();
        }

    }
}
