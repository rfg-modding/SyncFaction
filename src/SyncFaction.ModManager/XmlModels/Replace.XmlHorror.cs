using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class Replace
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

    [XmlAttribute(AttributeName = "NEWFILE")]
    public string NewFileUppercase
    {
        get => NewFile;
        set => NewFile = value;
    }

    [XmlAttribute(AttributeName = "newfile")]
    public string NewFileLowercase
    {
        get => NewFile;
        set => NewFile = value;
    }

    [XmlAttribute(AttributeName = "newFile")]
    public string NewFileCamelCase
    {
        get => NewFile;
        set => NewFile = value;
    }

    [XmlAttribute(AttributeName = "FILEUSERINPUT")]
    public string FileUserInputUppercase
    {
        get => FileUserInput;
        set => FileUserInput = value;
    }

    [XmlAttribute(AttributeName = "fileuserinput")]
    public string FileUserInputLowercase
    {
        get => FileUserInput;
        set => FileUserInput = value;
    }

    [XmlAttribute(AttributeName = "fileUserInput")]
    public string FileUserInputCamelCase
    {
        get => FileUserInput;
        set => FileUserInput = value;
    }

    [XmlAttribute(AttributeName = "NEWFILEUSERINPUT")]
    public string NewFileUserInputUppercase
    {
        get => NewFileUserInput;
        set => NewFileUserInput = value;
    }

    [XmlAttribute(AttributeName = "newfileuserinput")]
    public string NewFileUserInputLowercase
    {
        get => NewFileUserInput;
        set => NewFileUserInput = value;
    }

    [XmlAttribute(AttributeName = "newFileUserInput")]
    public string NewFileUserInputCamelCase
    {
        get => NewFileUserInput;
        set => NewFileUserInput = value;
    }
}
