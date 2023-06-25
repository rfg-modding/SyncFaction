using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

public interface IModViewModel
{
    string Name { get; }
    bool Selected { get; set; }

    IMod Mod { get; set; }
}
