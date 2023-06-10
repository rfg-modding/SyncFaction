using System.Collections.Immutable;

namespace SyncFaction.ModManager.Models;

public record VppOperations(IImmutableDictionary<string, FileSwapOperation> FileSwaps, IImmutableDictionary<string, XmlEditOperation> XmlEdits);