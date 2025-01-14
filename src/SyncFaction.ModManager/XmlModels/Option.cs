using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
public partial class Option : HasNestedXml, IOption
{
    public XmlNode? ValueHolder => NestedXml.Wrap();

    /// <summary>
    /// Display label
    /// </summary>
    [XmlAttribute]
    public string Name { get; set; }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}
