namespace SyncFaction.ModManager.Models;

public record VppOperations(ILookup<string, FileSwapOperation> FileSwaps, ILookup<string, XmlEditOperation> XmlEdits);
