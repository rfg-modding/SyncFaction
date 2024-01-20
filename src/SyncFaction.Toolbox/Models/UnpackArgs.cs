using Microsoft.Extensions.FileSystemGlobbing;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public record UnpackArgs(object Archive, string Name, DirectoryInfo Output, Matcher Matcher, UnpackSettings Settings, string RelativePath)
{
    public override string ToString()
    {
        return $"{Archive.GetType().Name} {Name}, {Output.FullName}, {RelativePath}, {Settings}";
    }
}
