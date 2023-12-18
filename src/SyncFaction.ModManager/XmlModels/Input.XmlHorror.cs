using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract partial class Input
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

    [XmlAttribute(AttributeName = "DISPLAYNAME")]
    public string DisplayNameUppercase
    {
        get => DisplayName;
        set => DisplayName = value;
    }

    [XmlAttribute(AttributeName = "displayname")]
    public string DisplayNameLowercase
    {
        get => DisplayName;
        set => DisplayName = value;
    }

    [XmlAttribute(AttributeName = "displayName")]
    public string DisplayNameCamelCase
    {
        get => DisplayName;
        set => DisplayName = value;
    }

    [XmlAttribute(AttributeName = "DESCRIPTION")]
    public string DescriptionUppercase
    {
        get => Description;
        set => Description = value;
    }

    [XmlAttribute(AttributeName = "description")]
    public string DescriptionLowercase
    {
        get => Description;
        set => Description = value;
    }
}
