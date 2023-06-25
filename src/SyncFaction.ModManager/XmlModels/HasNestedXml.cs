using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract class HasNestedXml
{
    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    [XmlIgnore]
    public string NestedString => string.Join("\n", NestedXml.Select(o => o.OuterXml).ToList());

    [XmlText]
    [XmlAnyElement]
    public List<XmlNode> NestedXml { get; set; }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeNestedXml() => false;
}
