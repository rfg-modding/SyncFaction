namespace SyncFaction.Toolbox.Models;

public record UnpackSettings(string ArchiveGlob, string FileGlob, string OutputPath, bool XmlFormat, bool Recursive, bool Textures, bool Metadata, bool Force);
