namespace SyncFaction.Packer;

public class LogicalFile
{
    public byte[] Content { get; set; }
    public string Name { get; set; }
    public string ParentName { get; set; }
    public int Order { get; set; }
}