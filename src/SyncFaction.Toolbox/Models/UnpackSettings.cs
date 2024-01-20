namespace SyncFaction.Toolbox.Models;

public record UnpackSettings(string ArchiveGlob, string FileGlob, string OutputPath, bool XmlFormat, bool Recursive, List<TextureFormat> Textures, bool Metadata, bool Force, int Parallel);
