using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public abstract class FileChange : IChange
{
    [XmlAttribute]
    public string File { get; set; }

    [XmlAttribute]
    public string NewFile { get; set; }

    [XmlAttribute]
    public string FileUserInput { get; set; }

    [XmlAttribute]
    public string NewFileUserInput { get; set; }

    public void ApplyUserInput(Dictionary<string, XmlNode> selectedValues)
    {
        if (!string.IsNullOrEmpty(FileUserInput))
        {
            if (!string.IsNullOrEmpty(File))
            {
                throw new ArgumentException($"Both {nameof(NewFile)} and {nameof(NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{NewFile}], [{NewFileUserInput}]");
            }

            // TODO support null (no-op)
            var holder = selectedValues[FileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            File = holder.ChildNodes[0].InnerText;
        }

        if (!string.IsNullOrEmpty(NewFileUserInput))
        {
            if (!string.IsNullOrEmpty(NewFile))
            {
                throw new ArgumentException($"Both {nameof(NewFile)} and {nameof(NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{NewFile}], [{NewFileUserInput}]");
            }
            // TODO support null (no-op)
            var holder = selectedValues[NewFileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            NewFile = holder.ChildNodes[0].InnerText;
        }
    }
}
