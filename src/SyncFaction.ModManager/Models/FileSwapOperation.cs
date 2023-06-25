using System.IO.Abstractions;

namespace SyncFaction.ModManager.Models;

public record FileSwapOperation(int Index, VppPath VppPath, IFileInfo Target) : IOperation;
