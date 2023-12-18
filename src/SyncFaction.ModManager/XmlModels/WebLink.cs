using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class WebLink
{
    [XmlAttribute]
    public string Name { get; set; } = null!;

    [XmlText]
    public string XmlText { get; set; } = null!;
}
