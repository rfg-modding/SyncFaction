using System.Collections;
using System.IO.Abstractions;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

[XmlRoot("Mod")]
public class ModInfo
{
    private List<IChange> changes = new();

    [XmlIgnore]
    public IDirectoryInfo WorkDir { get; set; }

    [XmlAttribute]
    public string Name { get; set; }

    public string Author { get; set; }

    public string Description { get; set; }

    public WebLink WebLink { get; set; }

    [XmlArrayItem(typeof(ListBox))]
    public List<Input> UserInput { get; set; }

    [XmlIgnore]
    public List<IChange> Changes {
        get => changes;
        set => changes = value;
    }

    /// <summary>
    /// Hack for XmlSerializer to work with interfaces
    /// </summary>
    [XmlArray("Changes")]
    [XmlArrayItem(typeof(Replace))]
    [XmlArrayItem(typeof(Edit))]
    public IList ChangesXmlHack
    {
        get => changes;
        set => changes = value.Cast<IChange>().ToList();
    }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeChangesXmlHack() => false;
}
