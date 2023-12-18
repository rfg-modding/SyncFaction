using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// User input with any value
/// </summary>
public class CustomOption : IOption
{
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

    public string? Value { get; set; }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}
