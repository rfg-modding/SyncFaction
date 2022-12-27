using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// No-op: do not apply changes if this is selected
/// </summary>
public class CustomOption : IOption
{
    public string Value { get; set; }

    public XmlNode? ValueHolder
    {
        get
        {
            // editable option gives only one text string as a node
            var fakeDoc = new XmlDocument();
            var holder = fakeDoc.GetHolderNode();
            var text = fakeDoc.CreateTextNode(Value);
            holder.AppendChild(text);
            return holder;
        }
    }
}