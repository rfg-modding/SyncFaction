namespace SyncFaction.Core.Models.Files;

public record Report(Dictionary<string, string> GameFiles, State State, string Version, string? LaseException);
