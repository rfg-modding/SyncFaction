using System.Collections;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

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