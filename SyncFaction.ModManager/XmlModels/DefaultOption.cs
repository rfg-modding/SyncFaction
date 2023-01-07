using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// No-op: do not apply changes if this is selected
/// </summary>
public class DefaultOption : IOption
{
    //public XmlNode? ValueHolder => null;
    public XmlNode? ValueHolder
    {
        get
        {
            var fakeDoc = new XmlDocument();
            var holder = fakeDoc.GetHolderNode();
            var text = fakeDoc.CreateTextNode(Extensions.NoOpName);
            holder.AppendChild(text);
            return holder;
        }
    }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}
