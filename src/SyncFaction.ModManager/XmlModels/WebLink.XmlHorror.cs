using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class WebLink
{
    [XmlAttribute(AttributeName = "NAME")]
    public string NameUppercase
    {
        get => Name;
        set => Name = value;
    }

    [XmlAttribute(AttributeName = "name")]
    public string NameLowercase
    {
        get => Name;
        set => Name = value;
    }
}
