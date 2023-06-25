using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Core.Models.FactionFiles;
using SyncFaction.Models;

namespace SyncFaction.ViewModels;

public partial class ViewModel
{
    public string GetHumanReadableVersion()
    {
        var sb = new StringBuilder();
        string value;
        lock (collectionLock)
        {
            var terraform = string.Join(", ", Model.TerraformUpdates);
            var rsl = string.Join(", ", Model.RslUpdates);
            value = $"terraform: {terraform}, rsl: {rsl}";
        }

        sb.Append(value);
        return sb.ToString();
    }

    public void UpdateUpdates(List<long> terraform, List<long> rsl)
    {
        lock (collectionLock)
        {
            Model.RemoteTerraformUpdates.Clear();
            Model.RemoteTerraformUpdates.AddRange(terraform);
            Model.RemoteRslUpdates.Clear();
            model.RemoteRslUpdates.AddRange(rsl);

            var newUpdates = Model.RemoteTerraformUpdates.Concat(model.RemoteRslUpdates).ToList();
            var currentUpdates = Model.TerraformUpdates.Concat(Model.RslUpdates).ToList();
            if (!currentUpdates.SequenceEqual(newUpdates))
            {
                log.LogWarning(@"You don't have latest patches installed!

# What is this?

Multiplayer mods depend on Terraform Patch and Script Loader. Even some singleplayer mods too! **It is highly recommended to have latest versions installed.**
This app is designed to keep players updated to avoid issues in multiplayer.
If you don't need this: install mods manually, suggest an improvement at Github or FF Discord, or enable dev mode.

# Press button below to update your game

Mod management will be available after updating.

Changelogs and info:
");
                var i = 1;
                foreach (var x in Model.RemoteTerraformUpdates)
                {
                    //log.LogInformation($"+ [Terraform patch part {i} (id {x})]({FormatUrl(x)})");
                    i++;
                }

                i = 1;
                foreach (var x in Model.RemoteRslUpdates)
                {
                    //log.LogInformation($"+ [Script Loader part {i} (id {x})]({FormatUrl(x)})");
                    i++;
                }

                UpdateRequired = true;
            }
            else
            {
                UpdateRequired = false;
            }
        }

        string FormatUrl(long x) => string.Format(CultureInfo.InvariantCulture, Constants.BrowserUrlTemplate, x);
    }

    public void AddOnlineMods(IReadOnlyList<IMod> mods) =>
        ViewAccessor.OnlineModListView.Dispatcher.Invoke(() =>
        {
            // lock whole batch for less noisy UI updates, inserting category by category
            lock (collectionLock)
            {
                foreach (var mod in mods)
                {
                    var vm = new OnlineModViewModel(mod) { Mod = mod };
                    vm.PropertyChanged += OnlineModChanged;
                    OnlineMods.Add(vm);
                }

                ResizeColumns(ViewAccessor.OnlineModListView);
            }
        });

    public void UpdateLocalMods(List<IMod> mods) =>
        ViewAccessor.LocalModListView.Dispatcher.Invoke(() =>
        {
            lock (collectionLock)
            {
                LocalMods.Clear();
                var tmp = new List<LocalModViewModel>();
                foreach (var mod in mods)
                {
                    var vm = new LocalModViewModel(mod);
                    vm.Status = LocalModStatus.Disabled;
                    vm.PropertyChanged += LocalModRecalculateOrder;
                    vm.PropertyChanged += LocalModChanged;
                    tmp.Add(vm);
                }

                var order = 1;
                foreach (var id in Model.AppliedMods)
                {
                    var vm = tmp.FirstOrDefault(x => x.Mod.Id == id);
                    if (vm is null)
                    {
                        throw new InvalidOperationException($"Unknown mod was applied before, id [{id}]. Restore game files!");
                    }

                    vm.Order = order++;
                    vm.Status = LocalModStatus.Enabled;
                }

                // NOTE: initial collection order is important!
                foreach (var x in tmp.OrderBy(x => x.Order).ThenBy(x => x.Name))
                {
                    LocalMods.Add(x);
                }

                ResizeColumns(ViewAccessor.OnlineModListView);
            }
        });

    public void LockedCollectionOperation(Action action)
    {
        lock (collectionLock)
        {
            action();
        }
    }

    public T LockedCollectionOperation<T>(Func<T> action)
    {
        lock (collectionLock)
        {
            return action();
        }
    }

    private void SetDesignTimeDefaults(bool isDesignTime)
    {
        if (isDesignTime)
        {
            // design-time defaults
            Model.DevMode = true;
            OnlineSelectedCount = 1;
            LocalSelectedCount = 2;
            GridLines = true;
            SelectedTab = Tab.Apply;
            GeneralFailure = true;

            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "just a mod",
                Category = Category.Local
            }));
            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "selected mod",
                Category = Category.Local
            }) { Selected = true });
            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "ready (dl+unp) mod",
                Category = Category.Local
            }) { Status = OnlineModStatus.Ready });
            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "mod in progress",
                Category = Category.Local
            }) { Status = OnlineModStatus.InProgress });
            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "failed mod",
                Category = Category.Local
            }) { Status = OnlineModStatus.Failed });
            OnlineMods.Add(new OnlineModViewModel(new Mod
            {
                Name = "mod with a ridiculously long name so nobody will read it entirely unless they really want to",
                Category = Category.Local
            }));

            LocalMods.Add(new LocalModViewModel(new Mod { Name = "just a mod" }));
            LocalMods.Add(new LocalModViewModel(new Mod { Name = "selected mod" }) { Selected = true });
            LocalMods.Add(new LocalModViewModel(new Mod { Name = "mod 3" }) { Order = 3 });
            LocalMods.Add(new LocalModViewModel(new Mod { Name = "mod 1" }) { Order = 1 });
            foreach (var localMod in LocalMods)
            {
                localMod.PropertyChanged += LocalModRecalculateOrder;
            }
        }
        else
        {
            Model.DevMode = false;
            OnlineMods.Clear();
            LocalMods.Clear();
            OnlineSelectedCount = 0;
            LocalSelectedCount = 0;
            GeneralFailure = false;
            GridLines = false;
            SelectedTab = Tab.Apply;
        }
    }

    private void LocalModsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LocalModCalculateOrder();
        lock (collectionLock)
        {
            LocalSelectedCount = LocalMods.Count(x => x.Status == LocalModStatus.Enabled);
        }
    }

    private void LocalModRecalculateOrder(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalModViewModel.Order) or nameof(LocalModViewModel.Selected))
        {
            return;
        }

        LocalModCalculateOrder();
    }

    /// <summary>
    /// Trigger lock/unlock for all interactive commands
    /// </summary>
    private void NotifyInteractiveCommands(object? _, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Interactive))
        {
            return;
        }

        foreach (var command in interactiveCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private void LocalModChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalModViewModel.Selected))
        {
            var target = sender as LocalModViewModel;
            // it's AsyncCommand, ok to call this way, awaited inside Execute()
            DisplayCommand.Execute(target);
        }

        else if (e.PropertyName == nameof(LocalModViewModel.Status))
        {
            lock (collectionLock)
            {
                LocalSelectedCount = LocalMods.Count(x => x.Status == LocalModStatus.Enabled);
            }
        }
    }

    private void ResizeColumns(ListView listView)
    {
        var view = listView.View as GridView;
        if (view == null || view.Columns.Count < 1)
        {
            return;
        }

        // Simulates column auto sizing as when double-clicking header border
        // it is very important to both insert and update UI sequentially in same thread
        // because otherwise callback for resize can be called before UI had time to update and we will still have wrong column width
        // CollectionChanged event does not help here: it is called after collection change but before UI update
        foreach (var column in view.Columns.Where(x => double.IsNaN(x.Width)))
        {
            column.Width = column.ActualWidth;
            column.Width = double.NaN;
        }
    }

    private void LocalModCalculateOrder()
    {
        lock (collectionLock)
        {
            var i = 1;
            foreach (var localMod in LocalMods)
            {
                localMod.Order = localMod.Status switch
                {
                    LocalModStatus.Enabled => i++,
                    LocalModStatus.Disabled => null,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }

    private void OnlineModChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnlineModViewModel.Selected))
        {
            return;
        }

        var target = sender as OnlineModViewModel;
        // it's AsyncCommand, ok to call this way: awaited inside Execute()
        DisplayCommand.Execute(target);
        lock (collectionLock)
        {
            OnlineSelectedCount = OnlineMods.Count(x => x.Selected);
        }
    }
}
