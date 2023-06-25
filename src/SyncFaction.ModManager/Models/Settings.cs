using System.Diagnostics.CodeAnalysis;

namespace SyncFaction.ModManager.Models;

public class Settings
{
    public Dictionary<long, Mod> Mods { get; set; } = new();

    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
    public class Mod
    {
        public Dictionary<string, ListBox> ListBoxes { get; set; } = new();
    }

    public class ListBox
    {
        public int SelectedIndex { get; set; }
        public string? CustomValue { get; set; }
    }
}
