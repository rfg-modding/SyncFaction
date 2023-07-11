using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class WebLink
{
    [XmlAttribute]
    public string Name { get; set; }

    [XmlText]
    public string XmlText { get; set; }
}
