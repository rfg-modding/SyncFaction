using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Option : HasNestedXml, IOption
{
    public XmlNode? ValueHolder => NestedXml.Wrap();

    /// <summary>
    /// Display label
    /// </summary>
    [XmlAttribute]
    public string Name { get; set; }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}
