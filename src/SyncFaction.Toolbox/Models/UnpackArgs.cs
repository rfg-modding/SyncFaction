using Microsoft.Extensions.FileSystemGlobbing;
using SyncFaction.Toolbox.Models;

namespace SyncFaction.Toolbox;

public record UnpackArgs(ArchiveType Type, FileInfo Archive, DirectoryInfo Output, Matcher Matcher, UnpackSettings Settings, string RelativePath);