namespace SyncFaction.ModManager.Models;

public interface IOperation
{
    public int Index { get; }
    public VppPath VppPath { get; }
}
