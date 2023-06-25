using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Edit : HasNestedXml, IChange
{
    [XmlAttribute]
    public string File { get; set; }

    [XmlAttribute]
    public string LIST_ACTION { get; set; }

    public IChange Clone() =>
        new Edit
        {
            File = File,
            LIST_ACTION = LIST_ACTION,
            NestedXml = NestedXml.Select(x => x.Clone()).ToList()
        };
}
