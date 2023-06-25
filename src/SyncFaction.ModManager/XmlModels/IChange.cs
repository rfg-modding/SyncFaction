namespace SyncFaction.ModManager.XmlModels;

public interface IChange
{
    public string File { get; set; }

    public IChange Clone();
}
