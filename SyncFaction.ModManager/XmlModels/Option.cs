using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Option : HasNestedXml, IOption
{
    /// <summary>
    /// Display label
    /// </summary>
    [XmlAttribute]
    public string Name { get; set; }

    public XmlNode? ValueHolder => NestedXml.Wrap();
}

public interface IOption
{
    XmlNode? ValueHolder { get; }
}

/// <summary>
/// No-op: do not apply changes if this is selected
/// </summary>
public class DefaultOption : IOption
{
    public XmlNode? ValueHolder => null;
}

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

public static class Extensions
{
    public static readonly string HolderName = "syncfaction_holder";

    public static XmlNode GetHolderNode(this XmlDocument doc) => doc.CreateNode(XmlNodeType.Element, HolderName, null);

    public static XmlNode Wrap(this IReadOnlyList<XmlNode> nodes)
    {
        if (nodes.Count == 0)
        {
            var fakeDoc = new XmlDocument();
            return fakeDoc.GetHolderNode();
        }

        // add common parent to all nodes
        var doc = nodes[0].OwnerDocument;
        var holder = doc.GetHolderNode();
        foreach (var node in nodes)
        {
            holder.AppendChild(node);
        }

        return holder;
    }
}
