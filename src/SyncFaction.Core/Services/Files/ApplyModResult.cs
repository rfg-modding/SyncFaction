namespace SyncFaction.Core.Services.Files;

public record ApplyModResult(IReadOnlyList<GameFile> ModifiedFiles, bool Success);