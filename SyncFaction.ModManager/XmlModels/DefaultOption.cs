using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

/// <summary>
/// No-op: do not apply changes if this is selected
/// </summary>
public class DefaultOption : IOption
{
    public XmlNode? ValueHolder => null;
}