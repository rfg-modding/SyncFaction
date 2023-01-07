using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

public static class Extensions
{
    public static readonly string HolderName = "syncfaction_holder";

    public static readonly string NoOpName = "syncfaction_noop";

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
