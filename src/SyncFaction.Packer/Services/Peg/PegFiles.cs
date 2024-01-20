namespace SyncFaction.Packer.Services.Peg;

public record PegFiles(FileInfo Cpu, FileInfo Gpu)
{
    public static PegFiles? FromExistingFile(FileInfo input)
    {
        if (!input.Exists)
        {
            return null;
        }

        var ext = input.Extension.ToLowerInvariant();
        var pairExt = ext switch
        {
            ".cpeg_pc" => ".gpeg_pc",
            ".gpeg_pc" => ".cpeg_pc",
            ".cvbm_pc" => ".gvbm_pc",
            ".gvbm_pc" => ".cvbm_pc",
            _ => null
        };
        if (pairExt is null)
        {
            return null;
        }

        var pairPath = Path.ChangeExtension(input.FullName, pairExt);
        var pair = new FileInfo(pairPath);
        if (!pair.Exists)
        {
            return null;
        }

        var first = ext.StartsWith(".c");
        return first ? new(input, pair) : new(pair, input);
    }

    public PegStreams OpenRead()
    {
        var c = Cpu.OpenRead();
        var g = Gpu.OpenRead();
        return new PegStreams(c, g);
    }

    public PegStreams OpenWrite()
    {
        var c = Cpu.OpenWrite();
        var g = Gpu.OpenWrite();
        return new PegStreams(c, g);
    }

    public string FullName => Cpu.FullName;
}