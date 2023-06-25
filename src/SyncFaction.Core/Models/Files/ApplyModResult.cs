namespace SyncFaction.Core.Models.Files;

public record ApplyModResult(IReadOnlyList<GameFile> ModifiedFiles, bool Success);
