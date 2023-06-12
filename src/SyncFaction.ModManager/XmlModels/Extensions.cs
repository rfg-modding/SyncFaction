using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

public static class Extensions
{
    public const string HolderName = "syncfaction_holder";

    public static readonly string NoOpName = "syncfaction_noop";

    public static XmlNode GetHolderNode(this XmlDocument doc, string nodeName=HolderName) => doc.CreateNode(XmlNodeType.Element, nodeName, null);

    public static XmlNode Wrap(this IReadOnlyList<XmlNode> nodes, string nodeName=HolderName)
    {
        if (nodes.Count == 0)
        {
            var fakeDoc = new XmlDocument();
            return fakeDoc.GetHolderNode();
        }

        // add common parent to all nodes
        var doc = nodes[0].OwnerDocument;
        var holder = doc.GetHolderNode(nodeName);
        foreach (var node in nodes)
        {
            holder.AppendChild(node);
        }

        return holder;
    }

    public static XmlNode Wrap(this XmlNode node, string nodeName = HolderName)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var doc = node.OwnerDocument;
        var holder = doc.GetHolderNode(nodeName);
        holder.AppendChild(node);

        return holder;
    }
}
