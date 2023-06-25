using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract class Input
{
    [XmlIgnore]
    public abstract XmlNode SelectedValue { get; }

    [XmlAttribute]
    public string Name { get; set; }

    [XmlAttribute]
    public string DisplayName { get; set; }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeSelectedValue() => false;
}
