using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// No-op: do not apply changes if this is selected
/// </summary>
public class CustomOption : IOption
{
    public string? Value { get; set; }

    public XmlNode? ValueHolder
    {
        get
        {
            // TODO: editable option as one text string node or an xml structure?
            var fakeDoc = new XmlDocument();
            var holder = fakeDoc.GetHolderNode();
            //var text = fakeDoc.CreateTextNode(Value);
            holder.InnerXml = Value ?? string.Empty;
            return holder;
        }
    }

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}
