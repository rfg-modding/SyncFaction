using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class Edit
{
    [XmlAttribute(AttributeName = "FILE")]
    public string FileUppercase
    {
        get => File;
        set => File = value;
    }

    [XmlAttribute(AttributeName = "file")]
    public string FileLowercase
    {
        get => File;
        set => File = value;
    }
}
