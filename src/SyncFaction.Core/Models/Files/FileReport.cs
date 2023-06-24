namespace SyncFaction.Core.Models;

public record FileReport(
    string Path,
    long Size,
    string? Hash,
    DateTime Created,
    DateTime Modified,
    DateTime Accessed
)
{
    public override string ToString()
    {
        return $"{Size,12} bytes, sha256 {Hash,64}, created {Created:s}, modified  {Modified:s}, accessed {Accessed:s}";
    }
}
