using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Replace : IChange
{
    [XmlAttribute]
    public string File { get; set; }

    [XmlAttribute]
    public string NewFile { get; set; }

    [XmlAttribute]
    public string FileUserInput { get; set; }

    [XmlAttribute]
    public string NewFileUserInput { get; set; }

    public IChange Clone()
    {
        return new Replace()
        {
            File = File,
            NewFile = NewFile,
            FileUserInput = FileUserInput,
            NewFileUserInput = NewFileUserInput
        };
    }


}
