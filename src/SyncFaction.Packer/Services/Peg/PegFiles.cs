namespace SyncFaction.Packer.Services.Peg;

public record PegFiles(FileInfo Cpu, FileInfo Gpu)
{
    public static PegFiles? FromExistingFile(FileInfo input)
    {
        if (!input.Exists)
        {
            return null;
        }

        var cpu = GetCpuFileName(input.FullName);
        var gpu = GetGpuFileName(input.FullName);
        if (cpu is null || gpu is null)
        {
            return null;
        }

        var cpuFile = new FileInfo(cpu);
        var gpuFile = new FileInfo(gpu);
        return new(cpuFile, gpuFile);
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

    /// <summary>
    /// Works for full and relative filenames
    /// </summary>
    public static string? GetCpuFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant()[1..];
        var cpuExt = ext switch
        {
            "cpeg_pc" => "cpeg_pc",
            "gpeg_pc" => "cpeg_pc",
            "cvbm_pc" => "cvbm_pc",
            "gvbm_pc" => "cvbm_pc",
            _ => null
        };
        return cpuExt is null
            ? null
            : Path.ChangeExtension(fileName, cpuExt);
    }

    /// <summary>
    /// Works for full and relative filenames
    /// </summary>
    public static string? GetGpuFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant()[1..];
        var gpuExt = ext switch
        {
            "cpeg_pc" => "gpeg_pc",
            "gpeg_pc" => "gpeg_pc",
            "cvbm_pc" => "gvbm_pc",
            "gvbm_pc" => "gvbm_pc",
            _ => null
        };
        return gpuExt is null
            ? null
            : Path.ChangeExtension(fileName, gpuExt);
    }
}
