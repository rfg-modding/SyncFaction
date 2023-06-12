using System.Xml;
using Microsoft.Extensions.Logging;
using SyncFaction.Core;
using SyncFaction.Packer;

namespace SyncFaction.Toolbox;

public class Commands
{
    private readonly IVppArchiver vppArchiver;
    private readonly ILogger<Commands> log;

    public Commands(IVppArchiver vppArchiver, ILogger<Commands> log)
    {
        this.vppArchiver = vppArchiver;
        this.log = log;
    }

    public async Task Unpack(FileInfo archive, DirectoryInfo dir, bool xmlFormat, bool force, CancellationToken token)
    {
        log.LogInformation("Unpacking [{archive}] to [{dir}]", archive.FullName, dir.FullName);

        if (!archive.Exists)
        {
            throw new FileNotFoundException("Archive does not exist", archive.FullName);
        }

        var outputDir = new DirectoryInfo(Path.Combine(dir.FullName, archive.Name));
        if (outputDir.Exists)
        {
            if (outputDir.EnumerateFileSystemInfos().Any() && !force)
            {
                throw new ArgumentException($"Output directory [{outputDir.FullName}] is not empty. Use --force to overwrite");
            }

            outputDir.Delete(true);
            outputDir.Refresh();
        }

        outputDir.Create();
        outputDir.Refresh();

        await using var src = archive.OpenRead();
        var vpp = await vppArchiver.UnpackVpp(src, archive.Name, token);
        foreach (var logicalFile in vpp.LogicalFiles)
        {
            var file = new FileInfo(Path.Combine(outputDir.FullName, logicalFile.Name));
            if (file.Exists)
            {
                throw new InvalidOperationException($"File [{file.FullName}] exists, can not unpack. Duplicate entries in archive?");
            }

            //await using var fileStream = file.OpenWrite();
            //await logicalFile.Content.CopyToAsync(fileStream, token);
            await using var fileStream = file.OpenWrite();
            if (xmlFormat && logicalFile.Name.ToLowerInvariant().EndsWith(".xtbl"))
            {
                // reformat original xml for readability
                var xml = new XmlDocument();
                using var reader = new StreamReader(logicalFile.Content);
                xml.Load(reader);
                using var ms = new MemoryStream();
                xml.SerializeToMemoryStream(ms);
                await ms.CopyToAsync(fileStream, token);
            }
            else
            {
                await logicalFile.Content.CopyToAsync(fileStream, token);
            }
        }
    }

    public async Task Get(FileInfo archive, string fileName, FileInfo output, bool xmlFormat, bool force, CancellationToken token)
    {
        log.LogInformation("Extracting [{archive}/{fileName}] to [{output}], xmlFormat={xmlFormat}, force={force}", archive.FullName, fileName, output.FullName, xmlFormat, force);

        if (!archive.Exists)
        {
            throw new FileNotFoundException("Archive does not exist", archive.FullName);
        }

        if (output.Exists)
        {
            if (!force)
            {
                throw new ArgumentException($"Output file [{output.FullName}] exists. Use --force to overwrite");
            }

            output.Delete();
            output.Refresh();
        }

        await using var src = archive.OpenRead();
        var vpp = await vppArchiver.UnpackVpp(src, archive.Name, token);
        var logicalFile = vpp.LogicalFiles.Single(x => x.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));
        Directory.CreateDirectory(output.Directory.FullName);
        await using var fileStream = output.OpenWrite();
        if (xmlFormat)
        {
            // reformat original xml for readability
            var xml = new XmlDocument();
            using var reader = new StreamReader(logicalFile.Content);
            xml.Load(reader);
            using var ms = new MemoryStream();
            xml.SerializeToMemoryStream(ms);
            await ms.CopyToAsync(fileStream, token);
        }
        else
        {
            await logicalFile.Content.CopyToAsync(fileStream, token);
        }
    }
}
