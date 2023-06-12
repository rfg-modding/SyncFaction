using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager;
using SyncFaction.ModManager.Models;
using SyncFaction.Packer;

namespace SyncFaction.Core.Services.Files;

public class ModInstaller : IModInstaller
{
    private readonly ILogger<ModInstaller> log;
    private readonly IVppArchiver vppArchiver;
    private readonly ModTools modTools;
    private readonly IXdeltaFactory xdeltaFactory;

    public ModInstaller(IVppArchiver vppArchiver, ModTools modTools, IXdeltaFactory xdeltaFactory, ILogger<ModInstaller> log)
    {
        this.vppArchiver = vppArchiver;
        this.modTools = modTools;
        this.xdeltaFactory = xdeltaFactory;
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
            _ => ApplyNewFile(gameFile, modFile),
        };

        gameFile.FileInfo.Refresh();
        return result;
    }

    internal virtual bool Skip(GameFile gameFile, IFileInfo modFile)
    {
        log.LogInformation($"+ Skipped unsupported mod file `{modFile.Name}`");
        return true;
    }

    internal virtual async Task<bool> ApplyXdelta(GameFile gameFile, IFileInfo modFile, CancellationToken token)
    {
        var tmpFile = gameFile.GetTmpFile();
        var srcFile = gameFile.FileInfo;
        var result = await ApplyXdeltaInternal(srcFile, modFile, tmpFile, token);
        tmpFile.Refresh();
        tmpFile.MoveTo(gameFile.AbsolutePath, true);
        return result;
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
            decoder.ProgressChanged += progress => { token.ThrowIfCancellationRequested(); };
            decoder.Run();

            log.LogInformation($"+ **Patched** `{modFile.Name}`");
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, $"XDelta failed: [{srcFile.FullName}] + [{modFile.FullName}] -> [{dstFile.FullName}]");
            throw;
        }
    }

    internal virtual bool ApplyNewFile(GameFile gameFile, IFileInfo modFile)
    {
        EnsureDirectoriesCreated(gameFile.FileInfo);
        modFile.CopyTo(gameFile.FileInfo.FullName, true);
        log.LogInformation($"+ Copied `{modFile.Name}`");
        return true;
    }

    public async Task<bool> ApplyVppDirectoryMod(GameFile gameFile, IDirectoryInfo vppDir, CancellationToken token)
    {
        var modFiles = vppDir.EnumerateFiles("*", SearchOption.AllDirectories).ToDictionary(x => x.FileSystem.Path.GetRelativePath(vppDir.FullName, x.FullName).ToLowerInvariant());
        LogicalArchive archive;
        List<LogicalFile> logicalFiles;
        await using (var src = gameFile.FileInfo.OpenRead())
        {
            log.LogInformation("Unpacking {vpp}", gameFile.NameExt);
            archive = await vppArchiver.UnpackVpp(src, gameFile.FileInfo.Name, token);
            var usedKeys = new HashSet<string>();
            var order = 0;

            async IAsyncEnumerable<LogicalFile> WalkArchive()
            {
                // modifying stuff in ram while reading. do we have 2 copies now?
                foreach (var logicalFile in archive.LogicalFiles)
                {
                    token.ThrowIfCancellationRequested();
                    var key = logicalFile.Name;
                    order = logicalFile.Order;
                    if (modFiles.TryGetValue(key, out var modFile))
                    {
                        log.LogInformation("Replacing file {file} in {vpp}", key, archive.Name);
                        usedKeys.Add(key);
                        var modSrc = modFile.OpenRead();
                        yield return logicalFile with {Content = modSrc, CompressedContent = null};
                    }
                    else
                    {
                        yield return logicalFile;
                    }
                }
            }

            logicalFiles = await WalkArchive().ToListAsync(token);
            // append new files
            var newFileKeys = modFiles.Keys.Except(usedKeys).OrderBy(x => x);
            foreach (var key in newFileKeys)
            {
                log.LogInformation("Adding file {file} in {vpp}", key, archive.Name);
                order++;
                var modFile = modFiles[key];
                var modSrc = modFile.OpenRead();
                logicalFiles.Add(new LogicalFile(modSrc, key, order, null, null));
            }
        }

        // write
        await using var dst = gameFile.FileInfo.Open(FileMode.Truncate);
        log.LogInformation("Packing {vpp}", gameFile.NameExt);
        await vppArchiver.PackVpp(archive with {LogicalFiles = logicalFiles}, dst, token);
        log.LogInformation("Finished with {vpp}", gameFile.NameExt);
        // GC magic!
        logicalFiles.Clear();
        GC.Collect();
        return true;
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
                var logicalFiles = archive.LogicalFiles.Select(x => ApplyPatches(x, vppOperations, disposables, token));

                await using (var dst = tmpFile.OpenWrite())
                {
                    await vppArchiver.PackVpp(archive with {LogicalFiles = logicalFiles}, dst, token);
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
        tmpFile.Refresh();
        tmpFile.MoveTo(gameFile.AbsolutePath, true);
        log.LogInformation("Patched xmls inside [{file}]", gameFile.RelativePath);

        return true;
    }

    private void EnsureDirectoriesCreated(IFileInfo file)
    {
        file.FileSystem.Directory.CreateDirectory(file.Directory.FullName);
    }

    private LogicalFile ApplyPatches(LogicalFile file, VppOperations vppOperations, List<IDisposable> disposables, CancellationToken token)
    {
        // NOTE: it's important to swap files first, then edit xml contents!
        if (vppOperations.FileSwaps.TryGetValue(file.Name, out var swap))
        {
            log.LogDebug("swap [{key}] [{value}]", file.Name, swap.Target);
            var stream = swap.Target.OpenRead();
            disposables.Add(stream);
            file = file with {Content = stream, CompressedContent = null};
        }

        if (vppOperations.XmlEdits.TryGetValue(file.Name, out var edit))
        {
            var ext = file.Name.Split(".").Last().ToLowerInvariant();
            if (!KnownXmlExtensions.Contains(ext))
            {
                var extList = string.Join(", ", KnownXmlExtensions);
                throw new InvalidOperationException($"Can not edit file [{file.Name}]. Supported file extensions: [{extList}]");
            }

            log.LogDebug("edit [{key}] [{value}]", file.Name, edit.Action);
            // TODO mess with contents, rewind resulting stream
            // TODO move this to ModTools?
            var gameXml = new XmlDocument();
            gameXml.Load(file.Content);
            var xtblRoot = gameXml["root"]?["Table"];
            if (xtblRoot is null)
            {
                throw new ArgumentNullException(nameof(xtblRoot), "Invalid xtbl, missing [.root.Table] element");
            }
            //todo walk xml
            // TODO merging should copy attrs too

            switch (edit.ListAction)
            {
                case ListAction.Add:
                    ImportNodes(edit.Xml, xtblRoot);
                    break;
                case ListAction.Replace:
                    xtblRoot.RemoveAll();
                    ImportNodes(edit.Xml, xtblRoot);
                    break;
                case ListAction.CombineByField:
                    var criteria = edit.Action.Substring("combine_by_field:".Length);
                    foreach (var editChild in edit.Xml)
                    {
                        var matcher = CreateSubnodeValueMatcher(criteria, editChild);
                        CopyNodesToTargetIfMatched(editChild, xtblRoot, matcher);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var ms = new MemoryStream();
            disposables.Add(ms);
            gameXml.SerializeToMemoryStream(ms);
            file = file with {Content = ms, CompressedContent = null};
        }

        return file;
    }

    private static readonly ImmutableHashSet<string> KnownXmlExtensions = new HashSet<string>()
    {
        "xtbl",
        "dtdox",
        "gtodx"
    }.ToImmutableHashSet();




    private void ImportNodes(List<XmlNode> nodes, XmlElement target)
    {
        foreach (var node in nodes)
        {
            var imported = target.OwnerDocument.ImportNode(node, true);
            target.AppendChild(imported);
        }
    }

    private static void CopyNodesToTargetIfMatched(XmlNode source, XmlElement target, IXmlNodeMatcher matcher)
    {
        // look for first match
        foreach (XmlNode x in target.ChildNodes)
        {
            if (source.LocalName == x.LocalName && matcher.DoesNodeMatch(x))
            {
                // TODO descend recursively and merge
                //CopyNodesToTargetByListAction((XmlElement) source, (XmlElement) childNode);

                foreach (XmlNode y in source.ChildNodes)
                {
                    CopyNodeToTargetIfNeeded(y, x);
                }

                return;
            }
        }

        // if nothing matched, copy as new node
        var imported = target.OwnerDocument.ImportNode(source, true);
        target.AppendChild(imported);
    }

    public static void CopyNodeToTargetIfNeeded(XmlNode source, XmlNode target)
    {
        switch (source.NodeType)
        {
            case XmlNodeType.Element:
                // use existing or create new
                var newTarget = target[source.LocalName] ?? target.AppendChild(target.OwnerDocument.CreateElement(source.LocalName));
                foreach (var sourceChildNode in source.ChildNodes)
                {
                    CopyNodeToTargetIfNeeded(sourceChildNode as XmlNode, newTarget);
                }
                break;
            case XmlNodeType.Text:
                SetNodeXmlTextValue(target, source.InnerText);
                break;
        }
    }

    public static void SetNodeXmlTextValue(XmlNode node, string textString)
    {
        XmlText xmlText = GetNodeOrSubnodeText(node);

        if (textString == string.Empty)
        {
            if (xmlText is not null)
            {
                //If string empty and xmlText isn't null, delete xmlText node
                node.RemoveChild(xmlText);
            }
            // else, do nothing and return
        }
        else
        {
            if (xmlText is not null)
            {
                //If string not empty and xmlText != null, set it's value to textString
                xmlText.InnerText = textString;
            }
            else
            {
                //If xmlText is null then add XmlText subnode with the value of textString
                XmlText textNode = node.OwnerDocument.CreateTextNode(textString);
                node.AppendChild(textNode);
            }
        }
    }

    public static XmlText GetNodeOrSubnodeText(XmlNode node)
    {
        if (node.NodeType == XmlNodeType.Text)
            return (XmlText)node;

        foreach (XmlNode childNode in node.ChildNodes)
        {
            if (childNode.NodeType == XmlNodeType.Text)
                return (XmlText)childNode;
        }

        return null;
    }

    private static IXmlNodeMatcher CreateSubnodeValueMatcher(string criteria, XmlNode editChild)
    {
        if (string.IsNullOrWhiteSpace(criteria))
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        //example: "Name,_Editor\Category"
        var xPaths = criteria.Split(',').Select(FormatXPath).ToList();
        var value = GetSubnodeByXPath(editChild, xPaths[0])?.InnerText;
        if (string.IsNullOrWhiteSpace(value))
        {
            return new AlwaysFalseMatcher();
        }

        IXmlNodeMatcher matcher = new SubnodeValueMatcher(xPaths[0], value);
        foreach (var x in xPaths.Skip(1))
        {
            // TODO logic with AlwaysFalseMatcher and not checking next subnode results is weird but maybe it has some reasons?
            var result = GetSubnodeByXPath(editChild, x)?.InnerText ?? string.Empty;
            matcher = new AndMatcher(matcher, new SubnodeValueMatcher(x, result));
        }

        return matcher;
    }

    private static string FormatXPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(nameof(value));
        }
        var tags = value.Split(new[] {'\\'}, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", tags);
    }


    public static XmlNode? GetSubnodeByXPath(XmlNode? node, string xPath)
    {
        if (string.IsNullOrWhiteSpace(xPath))
        {
            return null;
        }

        if (node is null)
        {
            return null;
        }

        return node.SelectSingleNode(xPath);
    }

    interface IXmlNodeMatcher
    {
        bool DoesNodeMatch(XmlNode node);
    }

    class AlwaysFalseMatcher : IXmlNodeMatcher
    {
        public bool DoesNodeMatch(XmlNode node) => false;
    }

    class SubnodeValueMatcher : IXmlNodeMatcher
    {
        public string XPath;
        public string ExpectedValue;

        public SubnodeValueMatcher(string xPath, string expectedValue)
        {
            if (string.IsNullOrWhiteSpace(xPath))
            {
                throw new ArgumentNullException(nameof(xPath));
            }

            if (string.IsNullOrWhiteSpace(expectedValue))
            {
                throw new ArgumentNullException(nameof(expectedValue));
            }

            XPath = xPath;
            ExpectedValue = expectedValue;
        }

        public bool DoesNodeMatch(XmlNode node) => GetSubnodeByXPath(node, XPath)?.InnerText == ExpectedValue;

        public virtual string ToString() => XPath + "=" + ExpectedValue;
    }

    class AndMatcher : IXmlNodeMatcher
    {
        public IXmlNodeMatcher Matcher0;
        public IXmlNodeMatcher Matcher1;

        public AndMatcher(IXmlNodeMatcher matcher0, IXmlNodeMatcher matcher1)
        {
            Matcher0 = matcher0;
            Matcher1 = matcher1;
        }

        public bool DoesNodeMatch(XmlNode node) => Matcher0.DoesNodeMatch(node) && Matcher1.DoesNodeMatch(node);

        public virtual string ToString() => Matcher0.ToString() + " AND " + Matcher1.ToString();
    }
}
