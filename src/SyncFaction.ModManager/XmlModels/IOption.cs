using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

public interface IOption
{
    XmlNode? ValueHolder { get; }
}
