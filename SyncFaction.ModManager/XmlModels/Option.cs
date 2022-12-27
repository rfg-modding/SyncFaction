using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Option : HasNestedXml, IOption
{
    /// <summary>
    /// Display label
    /// </summary>
    [XmlAttribute]
    public string Name { get; set; }

    public XmlNode? ValueHolder => NestedXml.Wrap();
}