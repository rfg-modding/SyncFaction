namespace SyncFaction.Toolbox.Models;

public record UnpackSettings(string ArchiveGlob, string FileGlob, string OutputPath, bool XmlFormat, bool Recursive, List<Archiver.TextureFormat> Textures, bool Metadata, bool Force, int Parallel);
