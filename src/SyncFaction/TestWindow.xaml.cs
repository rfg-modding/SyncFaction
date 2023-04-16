using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SyncFaction;

public partial class TestWindow : Window
{

    public Foo foo = new();

    public TestWindow()
    {
        DataContext = foo;
        foo.Values.CollectionChanged += CollectionChanged;
        InitializeComponent();

        foo.Values.Add("aaa");
        foo.Values.Add("aaaaaa");
        foo.Values.Add("aaaaaaaaa");
        foo.Values.Add("aaaaaaaaaaaa");
        foo.Values.Add("aaaaaaaaaaaaaaa");
        foo.Values.Add("aaaaaaaaaaaaaaaaaa");
    }

    private void MainWindow_OnContentRendered(object? sender, EventArgs e)
    {
        foo.Values.Add("AAAAAAAAAAAAAAAAAAAAAAAA");
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        foo.Values.Add("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");
    }

    void CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var view = OnlineModList.View as GridView;
        AutoResizeGridViewColumns(view);
    }

    static void AutoResizeGridViewColumns(GridView? view)
    {
        if (view == null || view.Columns.Count < 1) return;
        // Simulates column auto sizing
        foreach (var column in view.Columns.Where(x => double.IsNaN(x.Width)))
        {
            column.Width = column.ActualWidth;
            column.Width = double.NaN;
        }
    }
}
