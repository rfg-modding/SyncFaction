using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class ListBox
{
    [XmlAttribute(AttributeName = "ALLOWCUSTOM")]
    public bool AllowCustomUppercase
    {
        get => AllowCustom;
        set => AllowCustom = value;
    }

    [XmlAttribute(AttributeName = "allowcustom")]
    public bool AllowCustomLowercase
    {
        get => AllowCustom;
        set => AllowCustom = value;
    }

    [XmlAttribute(AttributeName = "allowCustom")]
    public bool AllowCustomCamelCase
    {
        get => AllowCustom;
        set => AllowCustom = value;
    }

    [XmlAttribute(AttributeName = "SAMEOPTIONSAS")]
    public string SameOptionsAsUppercase
    {
        get => SameOptionsAs;
        set => SameOptionsAs = value;
    }

    [XmlAttribute(AttributeName = "sameoptionsas")]
    public string SameOptionsAsLowercase
    {
        get => SameOptionsAs;
        set => SameOptionsAs = value;
    }

    [XmlAttribute(AttributeName = "sameOptionsAs")]
    public string SameOptionsAsCamelCase
    {
        get => SameOptionsAs;
        set => SameOptionsAs = value;
    }

    [XmlElement(ElementName = "OPTION")]
    public List<Option> XmlOptionsUppercase
    {
        get => XmlOptions;
        set => XmlOptions = value;
    }

    [XmlElement(ElementName = "option")]
    public List<Option> XmlOptionsLowercase
    {
        get => XmlOptions;
        set => XmlOptions = value;
    }
}
