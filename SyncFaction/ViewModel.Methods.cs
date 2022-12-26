using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Data;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

public partial class ViewModel
{
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

            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "just a mod", Category = Category.Local}));
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "selected mod", Category = Category.Local}) {Selected = true});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "ready (dl+unp) mod", Category = Category.Local}) {Status = OnlineModStatus.Ready});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "mod in progress", Category = Category.Local}) {Status = OnlineModStatus.InProgress});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "failed mod", Category = Category.Local}) {Status = OnlineModStatus.Failed});
            OnlineMods.Add(new OnlineModViewModel(new Mod() {Name = "mod with a ridiculously long name so nobody will read it entirely unless they really want to", Category = Category.Local}));

            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "just a mod"}));
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "selected mod"}) {Selected = true});
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "mod 3"}) {Order = 3});
            LocalMods.Add(new LocalModViewModel(new Mod() {Name = "mod 1"}) {Order = 1});
            foreach (var localMod in LocalMods)
            {
                localMod.PropertyChanged += LocalModOnPropertyChanged;
            }
        }
        else
        {
            Model.DevMode = false;
            OnlineMods.Clear();
            LocalMods.Clear();
            OnlineSelectedCount = 0;
            LocalSelectedCount = 0;
            Failure = false;
            GridLines = false;
            SelectedTab = Tab.Apply;
        }
    }

    private void LocalModsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LocalModCalculateOrder();
    }

    private void LocalModOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

    /// <summary>
    /// Update json view for display and avoid infinite loop
    /// </summary>
    private void UpdateJsonView(object? _, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(JsonView))
        {
            return;

        }

        OnPropertyChanged(nameof(JsonView));
    }

    public void UpdateLocalMods(List<IMod> mods)
    {
        // TODO compare applied mods from state with current mods
        ViewAccessor.LocalModListView.Dispatcher.Invoke(() =>
        {
            LockedCollectionOperation(() =>
            {
                LocalMods.Clear();
                foreach (var mod in mods)
                {
                    var vm = new LocalModViewModel(mod);
                    vm.PropertyChanged += LocalModOnPropertyChanged;
                    vm.PropertyChanged += LocalModDisplay;
                    LocalMods.Add(vm);
                }
            });
        });
    }

    private void LocalModDisplay(object? sender, PropertyChangedEventArgs e)
    {
        // TODO: gets called twice for some reason
        // TODO: unable to deselect items, why?!
        if (e.PropertyName != nameof(LocalModViewModel.Selected))
        {
            return;
        }

        var target = sender as LocalModViewModel;
        // it's AsyncCommand, ok to call this way: awaited inside Execute()
        DisplayCommand.Execute(target);
    }

    public string GetHumanReadableCommunityVersion()
    {
        var sb = new StringBuilder();
        sb.Append("base: ");
        sb.Append(Model.CommunityPatch == 0 ? "not installed" : Model.CommunityPatch);
        sb.Append(", updates: ");
        string updates;
        lock (collectionLock)
        {
            updates = string.Join(", ", Model.CommunityUpdates);
        }
        sb.Append(updates == string.Empty ? "none" : updates);
        return sb.ToString();
    }

    public void UpdateUpdates(long newPatch, List<long> updates)
    {
        lock(collectionLock)
        {
            Model.NewCommunityPatch = newPatch;
            Model.NewCommunityUpdates.Clear();
            foreach (var u in updates)
            {
                Model.NewCommunityUpdates.Add(u);
            }

            if (Model.CommunityPatch != Model.NewCommunityPatch || !Model.CommunityUpdates.SequenceEqual(Model.NewCommunityUpdates))
            {
                log.LogWarning(@$"You don't have latest community patch installed!

# What is this?

Multiplayer mods depend on community patch and its updates. Even some singleplayer mods too! **It is highly recommended to have latest versions installed.**
This app is designed to keep players updated to avoid issues in multiplayer.
If you don't need this, install mods manually, suggest an improvement at Github or FF Discord, or enable dev mode.

# Press button below to update your game

Mod management will be available after updating.

Changelogs and info:
");
                log.LogInformation($"+ [Community patch base (id {Model.NewCommunityPatch})]({FormatUrl(Model.NewCommunityPatch)})");
                var i = 1;
                foreach (var update in Model.NewCommunityUpdates)
                {
                    log.LogInformation($"+ [Community patch update {i} (id {update})]({FormatUrl(update)})");
                    i++;
                }

                UpdateRequired = true;
            }
            else
            {
                UpdateRequired = false;
            }
        }


        string FormatUrl(long x) => string.Format(Constants.BrowserUrlTemplate, x);
    }

    public void AddModsWithViewResizeOnUiThread(IReadOnlyList<IMod> mods)
    {
        ViewAccessor.OnlineModListView.Dispatcher.Invoke(() =>
        {
            // lock whole batch for less noisy UI updates, inserting category by category
            lock (collectionLock)
            {
                foreach (var mod in mods)
                {
                    var vm = new OnlineModViewModel(mod)
                    {
                        Mod = mod,
                    };
                    vm.PropertyChanged += OnlilneModDisplayAndUpdateCount;
                    OnlineMods.Add(vm);
                }

                var view = ViewAccessor.OnlineModListView.View as GridView;
                if (view == null || view.Columns.Count < 1) return;
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
        });

    }

    private void LocalModCalculateOrder()
    {

        lock (collectionLock)
        {

            var enabled = LocalMods.Count(x => x.Status == LocalModStatus.Enabled);
            var disabled = LocalMods.Count(x => x.Status == LocalModStatus.Disabled);
            try
            {
                log.LogInformation($"collection changed event, length: {LocalMods.Count} / enabled {enabled} / disabled {disabled}");
            }
            catch (Exception)
            {
            }

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

    private void OnlilneModDisplayAndUpdateCount(object? sender, PropertyChangedEventArgs e)
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
}