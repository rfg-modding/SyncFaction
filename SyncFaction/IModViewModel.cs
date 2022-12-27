using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction;

public interface IModViewModel
{
    /// <inheritdoc OnlineModViewModelwModel.selected"/>
    bool Selected { get; set; }

    string Name { get; }
    IMod Mod { get; set; }
}