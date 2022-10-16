using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

public interface IChange
{
    public void ApplyUserInput(Dictionary<string, XmlNode> selectedValues);
}
