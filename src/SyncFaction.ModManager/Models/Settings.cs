using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.ModManager;

public class Settings
{
    public Dictionary<long, Mod> Mods { get; set; } = new();

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
