using System.Diagnostics.CodeAnalysis;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Why not?")]
public interface IModViewModel
{
    string Name { get; }
    bool Selected { get; set; }

    IMod Mod { get; set; }
}
