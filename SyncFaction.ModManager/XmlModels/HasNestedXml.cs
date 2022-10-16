using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract class HasNestedXml
{
    [XmlText]
    [XmlAnyElement]
    public List<XmlNode> NestedXml { get; set; }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    [XmlIgnore]
    public string NestedString => String.Join("\n", NestedXml.Select(o => o.OuterXml).ToList());

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeNestedXml() => false;
}
