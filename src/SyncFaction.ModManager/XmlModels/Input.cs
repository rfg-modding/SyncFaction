using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract partial class Input
{
    [XmlIgnore]
    public abstract XmlNode? SelectedValue { get; }

    [XmlAttribute]
    public string Name { get; set; }

    [XmlAttribute]
    public string DisplayName { get; set; }

    [XmlAttribute]
    public string Description { get; set; }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeSelectedValue() => false;
}
