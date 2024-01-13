using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// User input with a value to ignore related operations
/// </summary>
public class NopOption : IOption
{
    public XmlNode? ValueHolder
    {
        get
        {
            var fakeDoc = new XmlDocument();
            var holder = fakeDoc.GetHolderNode();
            holder.InnerXml = Extensions.NopName;
            return holder;
        }
    }

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeValueHolder() => false;
}