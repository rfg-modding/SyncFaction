using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract class Input
{
    [XmlAttribute]
    public string Name { get; set; }

    [XmlAttribute]
    public string DisplayName { get; set; }

    [XmlIgnore]
    public abstract XmlNode SelectedValue { get; }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeSelectedValue() => false;
}
