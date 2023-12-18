using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class ModInfo
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

    [XmlElement(ElementName = "AUTHOR")]
    public string AuthorUppercase
    {
        get => Author;
        set => Author = value;
    }

    [XmlElement(ElementName = "author")]
    public string AuthorLowercase
    {
        get => Author;
        set => Author = value;
    }

    [XmlElement(ElementName = "DESCRIPTION")]
    public string DescriptionUppercase
    {
        get => Description;
        set => Description = value;
    }

    [XmlElement(ElementName = "description")]
    public string DescriptionLowercase
    {
        get => Description;
        set => Description = value;
    }

    [XmlElement(ElementName = "WEBLINK")]
    public WebLink WebLinkUppercase
    {
        get => WebLink;
        set => WebLink = value;
    }

    [XmlElement(ElementName = "weblink")]
    public WebLink WebLinkLowercase
    {
        get => WebLink;
        set => WebLink = value;
    }

    [XmlElement(ElementName = "webLink")]
    public WebLink WebLinkCamelCase
    {
        get => WebLink;
        set => WebLink = value;
    }

    [XmlArray(ElementName = "USERINPUT")]
    public List<Input> UserInputUppercase
    {
        get => UserInput;
        set => UserInput = value;
    }

    [XmlArray(ElementName = "userinput")]
    public List<Input> UserInputLowercase
    {
        get => UserInput;
        set => UserInput = value;
    }

    [XmlArray(ElementName = "userInput")]
    public List<Input> UserInputCamelCase
    {
        get => UserInput;
        set => UserInput = value;
    }

    [XmlElement(ElementName = "CHANGES")]
    public Changes ChangesUppercase
    {
        get => Changes;
        set => Changes = value;
    }

    [XmlElement(ElementName = "changes")]
    public Changes ChangesLowercase
    {
        get => Changes;
        set => Changes = value;
    }
}
