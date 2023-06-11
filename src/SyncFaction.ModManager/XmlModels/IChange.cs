using System.Xml;

namespace SyncFaction.ModManager.XmlModels;

public interface IChange
{
    public string File { get; set; }

    public void ApplyUserInput(Dictionary<string, XmlNode> selectedValues);

    public IChange Clone();
}
