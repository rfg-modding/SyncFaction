using SyncFaction.Core.Services;

namespace SyncFaction.Core.Models;

public record Report(Dictionary<string, string> GameFiles, State State, string Version, string? LaseException);
