using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models;
using SyncFaction.Core.Models.Files;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.Services;
using SyncFaction.Packer.Models;
using SyncFaction.Packer.Services;

namespace SyncFaction.Core.Services.Files;

public class ModInstaller : IModInstaller
{
    private readonly ILogger<ModInstaller> log;
    private readonly IVppArchiver vppArchiver;
    private readonly IXdeltaFactory xdeltaFactory;
    private readonly XmlMagic xmlMagic;

    public ModInstaller(IVppArchiver vppArchiver, IXdeltaFactory xdeltaFactory, XmlMagic xmlMagic, ILogger<ModInstaller> log)
    {
        this.vppArchiver = vppArchiver;
        this.xdeltaFactory = xdeltaFactory;
        this.xmlMagic = xmlMagic;
        this.log = log;
    }

    public async Task<bool> ApplyFileMod(GameFile gameFile, IFileInfo modFile, CancellationToken token)
    {
        if (!modFile.IsModContent())
        {
            return Skip(gameFile, modFile);
        }

        var result = modFile.Extension.ToLowerInvariant() switch
        {
            ".xdelta" => await ApplyXdelta(gameFile, modFile, token),
            _ => ApplyNewFile(gameFile, modFile)
        };

        gameFile.FileInfo.Refresh();
        return result;
    }

    public async Task<bool> ApplyVppDirectoryMod(GameFile gameFile, IDirectoryInfo vppDir, CancellationToken token)
    {
        var tmpFile = gameFile.GetTmpFile();
        try
        {
            await ApplyVppDirectoryModInternal(gameFile, vppDir, tmpFile, token);
        }
        catch (Exception)
        {
            tmpFile.Refresh();
            if (tmpFile.Exists)
            {
                tmpFile.Delete();
                log.LogTrace("Cleaned up tmp file [{file}] after error", tmpFile.FullName);
            }

            throw;
        }

        tmpFile.Refresh();
        tmpFile.MoveTo(gameFile.AbsolutePath, true);
        log.LogInformation(Md.Bullet.Id(), "Patched files inside `{file}`", gameFile.RelativePath);
        return true;
    }

    private async Task ApplyVppDirectoryModInternal(GameFile gameFile, IDirectoryInfo vppDir, IFileInfo tmpFile, CancellationToken token)
    {
        var modFiles = vppDir
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(static x => !x.Name.Equals(Constants.DeleteFile, StringComparison.OrdinalIgnoreCase))
            .Where(static x => !x.Name.Equals(Constants.ArchiveOptionsFile, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.FileSystem.Path.GetRelativePath(vppDir.FullName, x.FullName).ToLowerInvariant());
        var deleteList = await ReadDeleteList(vppDir);
        var archiveOptions = await ReadArchiveOptions(vppDir);
        await using var src = gameFile.FileInfo.OpenRead();
        var archive = await vppArchiver.UnpackVpp(src, gameFile.Name, token);
        var disposables = new List<IDisposable>();
        try
        {
            var usedKeys = new HashSet<string>();
            var order = 0;
            var logicalFiles = WalkArchive().ToList();

            // append new files
            var newFileKeys = modFiles.Keys.Except(usedKeys).OrderBy(static x => x, StringComparer.Ordinal);
            foreach (var key in newFileKeys)
            {
                log.LogInformation(Md.Bullet.Id(), "Added `{file}` to `{vpp}`", key, archive.Name);
                var modFile = modFiles[key];
                var modSrc = modFile.OpenRead();
                disposables.Add(modSrc);
                var originalName = modFile.FileSystem.Path.GetRelativePath(vppDir.FullName, modFile.FullName);
                logicalFiles.Add(new LogicalFile(modSrc, originalName, order++, null, null));
            }

            var newMode = archiveOptions.Mode ?? archive.Mode;
            log.LogTrace("Archive mode: [{oldMode}] => [{newMode}]", archive.Mode, newMode);
            await using var dst = tmpFile.OpenWrite();
            await vppArchiver.PackVpp(archive with
                {
                    LogicalFiles = logicalFiles,
                    Mode = newMode
                },
                dst,
                token);

            IEnumerable<LogicalFile> WalkArchive()
            {
                // modifying stuff in ram while reading. do we have 2 copies now?
                foreach (var logicalFile in archive.LogicalFiles)
                {
                    token.ThrowIfCancellationRequested();
                    var key = logicalFile.Name.ToLowerInvariant();
                    //order = logicalFile.Order;
                    if (modFiles.TryGetValue(key, out var modFile))
                    {
                        log.LogInformation(Md.Bullet.Id(), "Replaced `{file}` in `{vpp}`", key, archive.Name);
                        usedKeys.Add(key);
                        var modSrc = modFile.OpenRead();
                        disposables.Add(modSrc);
                        yield return logicalFile with
                        {
                            Content = modSrc,
                            CompressedContent = null,
                            Order = order++
                        };
                    }
                    else if (deleteList.Contains(logicalFile.Name.ToLowerInvariant()))
                    {
                        log.LogInformation(Md.Bullet.Id(), "Deleted `{file}` in `{vpp}`", key, archive.Name);
                    }
                    else
                    {
                        yield return logicalFile with
                        {
                            Order = order++
                        };
                    }
                }
            }
        }
        finally
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }

    private async Task<ImmutableHashSet<string>> ReadDeleteList(IDirectoryInfo vppDir)
    {
        var deleteFile = vppDir.EnumerateFiles("*").FirstOrDefault(static x => x.Name.Equals(Constants.DeleteFile, StringComparison.OrdinalIgnoreCase));
        if (deleteFile == null)
        {
            return ImmutableHashSet<string>.Empty;
        }

        log.LogTrace("Loose vpp directory has delete list [{file}]", deleteFile.FullName);
        await using var stream = deleteFile.OpenRead();
        var deleteList = JsonSerializer.Deserialize<List<string>>(stream, JsonOptions)!;
        log.LogTrace("Delete list has [{count}] entries", deleteList.Count);
        return deleteList
            .Select(static x => x.ToLowerInvariant())
            .ToImmutableHashSet();
    }

    private async Task<ArchiveOptions> ReadArchiveOptions(IDirectoryInfo vppDir)
    {
        var optionsFile = vppDir.EnumerateFiles("*").FirstOrDefault(static x => x.Name.Equals(Constants.ArchiveOptionsFile, StringComparison.OrdinalIgnoreCase));
        if (optionsFile == null)
        {
            return new ArchiveOptions(default);
        }

        log.LogTrace("Loose vpp directory has archive options [{file}]", optionsFile.FullName);
        await using var stream = optionsFile.OpenRead();
        var archiveOptions = JsonSerializer.Deserialize<ArchiveOptions>(stream, JsonOptions)!;
        log.LogTrace("Archive options: {archiveOptions}", archiveOptions);
        return archiveOptions;
    }

    public async Task<bool> ApplyModInfo(GameFile gameFile, VppOperations vppOperations, CancellationToken token)
    {
        var tmpFile = gameFile.GetTmpFile();
        await using (var src = gameFile.FileInfo.OpenRead())
        {
            var archive = await vppArchiver.UnpackVpp(src, gameFile.Name, token);
            var disposables = new List<IDisposable>();
            try
            {
                // TODO allow <Replace> to add files if they are not found in archive
                var logicalFiles = archive.LogicalFiles.Select(x => xmlMagic.ApplyPatches(x, vppOperations, disposables, token));
                await using (var dst = tmpFile.OpenWrite())
                {
                    await vppArchiver.PackVpp(archive with { LogicalFiles = logicalFiles }, dst, token);
                }
            }

            catch (Exception)
            {
                tmpFile.Refresh();
                if (tmpFile.Exists)
                {
                    tmpFile.Delete();
                    log.LogTrace("Cleaned up tmp file [{file}] after error", tmpFile.FullName);
                }

                throw;
            }
            finally
            {
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
            }
        }

        tmpFile.Refresh();
        tmpFile.MoveTo(gameFile.AbsolutePath, true);
        log.LogInformation(Md.Bullet.Id(), "Patched contents of [{file}]", gameFile.RelativePath);

        return true;
    }

    internal virtual bool Skip(GameFile gameFile, IFileInfo modFile)
    {
        log.LogInformation(Md.Bullet.Id(), "Skipped unsupported mod file `{file}`", modFile.Name);
        return true;
    }

    internal virtual async Task<bool> ApplyXdelta(GameFile gameFile, IFileInfo modFile, CancellationToken token)
    {
        var tmpFile = gameFile.GetTmpFile();
        try
        {
            var srcFile = gameFile.FileInfo;
            var result = await ApplyXdeltaInternal(srcFile, modFile, tmpFile, token);
            tmpFile.Refresh();
            tmpFile.MoveTo(gameFile.AbsolutePath, true);
            log.LogInformation(Md.Bullet.Id(), "Patched `{file}` into `{gameFile}`", modFile.Name, gameFile.RelativePath);
            return result;
        }
        catch (Exception)
        {
            tmpFile.Refresh();
            if (tmpFile.Exists)
            {
                tmpFile.Delete();
                log.LogTrace("Cleaned up tmp file [{file}] after error", tmpFile.FullName);
            }

            throw;
        }
    }

    internal virtual bool ApplyNewFile(GameFile gameFile, IFileInfo modFile)
    {
        EnsureDirectoriesCreated(gameFile.FileInfo);
        modFile.CopyTo(gameFile.FileInfo.FullName, true);
        log.LogInformation(Md.Bullet.Id(), "Copied `{file}` to `{gameFile}`", modFile.Name, gameFile.RelativePath);
        return true;
    }

    private async Task<bool> ApplyXdeltaInternal(IFileInfo srcFile, IFileInfo modFile, IFileInfo dstFile, CancellationToken token)
    {
        await using var srcStream = srcFile.OpenRead();
        await using var patchStream = modFile.OpenRead();
        await using var dstStream = dstFile.Open(FileMode.Create, FileAccess.ReadWrite);

        // TODO make it really async?
        try
        {
            using var decoder = xdeltaFactory.Create(srcStream, patchStream, dstStream);
            // TODO log progress
            decoder.ProgressChanged += _ => { token.ThrowIfCancellationRequested(); };
            decoder.Run();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "XDelta failed: [{src}] + [{mod}] -> [{dst}]", srcFile.FullName, modFile.FullName, dstFile.FullName);
            throw;
        }
    }

    private static void EnsureDirectoriesCreated(IFileInfo file) => file.FileSystem.Directory.CreateDirectory(file.Directory!.FullName);

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions() { Converters = { new JsonStringEnumConverter() } };
}
