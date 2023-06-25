using System.Collections;
using System.IO.Abstractions;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

[XmlRoot("Mod")]
public class ModInfo
{
    [XmlIgnore]
    public IDirectoryInfo WorkDir { get; set; }

    [XmlAttribute]
    public string Name { get; set; }

    public string Author { get; set; }

    public string Description { get; set; }

    public WebLink WebLink { get; set; }

    [XmlArrayItem(typeof(ListBox))]
    public List<Input> UserInput { get; set; }

    public Changes Changes { get; set; }

    [XmlIgnore]
    public IReadOnlyList<IChange> TypedChanges { get; set; }
}

public class Changes : HasNestedXml
{
}

[XmlRoot(Extensions.HolderName)]
public class TypedChangesHolder
{
    [XmlIgnore]
    public List<IChange> TypedChanges { get; set; } = new();

    /// <summary>
    /// Hack for XmlSerializer to work with interfaces
    /// </summary>
    [XmlArray(nameof(TypedChanges))]
    [XmlArrayItem(typeof(Replace))]
    [XmlArrayItem(typeof(Edit))]
    public IList TypedChangesXmlHack
    {
        get => TypedChanges;
        set => TypedChanges = value.Cast<IChange>().ToList();
    }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeTypedChangesXmlHack() => false;
}
