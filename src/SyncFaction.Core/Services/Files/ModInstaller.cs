using System.IO.Abstractions;
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
        var modFiles = vppDir.EnumerateFiles("*", SearchOption.AllDirectories).ToDictionary(x => x.FileSystem.Path.GetRelativePath(vppDir.FullName, x.FullName).ToLowerInvariant());
        await using var src = gameFile.FileInfo.OpenRead();
        var archive = await vppArchiver.UnpackVpp(src, gameFile.Name, token);
        var disposables = new List<IDisposable>();
        try
        {
            var usedKeys = new HashSet<string>();
            var order = 0;
            var logicalFiles = WalkArchive().ToList();

            // append new files
            var newFileKeys = modFiles.Keys.Except(usedKeys).OrderBy(static x => x);
            foreach (var key in newFileKeys)
            {
                log.LogInformation(Md.Bullet.Id(), "Added `{file}` to `{vpp}`", key, archive.Name);
                order++;
                var modFile = modFiles[key];
                var modSrc = modFile.OpenRead();
                disposables.Add(modSrc);
                logicalFiles.Add(new LogicalFile(modSrc, key, order, null, null));
            }

            await using var dst = tmpFile.OpenWrite();
            await vppArchiver.PackVpp(archive with { LogicalFiles = logicalFiles }, dst, token);

            IEnumerable<LogicalFile> WalkArchive()
            {
                // modifying stuff in ram while reading. do we have 2 copies now?
                foreach (var logicalFile in archive.LogicalFiles)
                {
                    token.ThrowIfCancellationRequested();
                    var key = logicalFile.Name;
                    order = logicalFile.Order;
                    if (modFiles.TryGetValue(key, out var modFile))
                    {
                        log.LogInformation(Md.Bullet.Id(), "Replaced `{file}` in `{vpp}`", key, archive.Name);
                        usedKeys.Add(key);
                        var modSrc = modFile.OpenRead();
                        disposables.Add(modSrc);
                        yield return logicalFile with
                        {
                            Content = modSrc,
                            CompressedContent = null
                        };
                    }
                    else
                    {
                        yield return logicalFile;
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

    public async Task<bool> ApplyModInfo(GameFile gameFile, VppOperations vppOperations, CancellationToken token)
    {
        var tmpFile = gameFile.GetTmpFile();
        await using (var src = gameFile.FileInfo.OpenRead())
        {
            var archive = await vppArchiver.UnpackVpp(src, gameFile.Name, token);
            var disposables = new List<IDisposable>();
            try
            {
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
}
