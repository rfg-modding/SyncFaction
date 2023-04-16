using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

public interface IModViewModel
{
    bool Selected { get; set; }

    string Name { get; }

    IMod Mod { get; set; }
}
