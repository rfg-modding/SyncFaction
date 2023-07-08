using System.Xml;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.XmlModels;
using SyncFaction.Packer.Models;

namespace SyncFaction.ModManager.Services;

public class XmlMagic
{
    private readonly XmlHelper xmlHelper;
    private readonly ILogger<XmlMagic> log;

    public XmlMagic(XmlHelper xmlHelper, ILogger<XmlMagic> log)
    {
        this.xmlHelper = xmlHelper;
        this.log = log;
    }

    public LogicalFile ApplyPatches(LogicalFile file, VppOperations vppOperations, List<IDisposable> disposables, CancellationToken token)
    {
        // NOTE: it's important to swap files first, then edit xml contents!
        var swaps = vppOperations.FileSwaps[file.Name];
        foreach (var swap in swaps)
        {
            token.ThrowIfCancellationRequested();
            var stream = swap.Target.OpenRead();
            disposables.Add(stream);
            file = file with
            {
                Content = stream,
                CompressedContent = null
            };
            log.LogTrace("File swap: [{key}] [{value}]", file.Name, swap.Target);
        }

        var edits = vppOperations.XmlEdits[file.Name];
        foreach (var edit in edits)
        {
            token.ThrowIfCancellationRequested();
            var ext = file.Name.Split(".").Last().ToLowerInvariant();
            if (!xmlHelper.KnownXmlExtensions.Contains(ext))
            {
                var extList = string.Join(", ", xmlHelper.KnownXmlExtensions);
                throw new InvalidOperationException($"Can not edit file [{file.Name}]. Supported file extensions: [{extList}]");
            }

            log.LogTrace("XML Edit: [{key}] [{value}]", file.Name, edit.Action);
            var gameXml = ReadXmlDocument(file);
            var xtblRoot = gameXml["root"]?["Table"];
            if (xtblRoot is null)
            {
                throw new ArgumentNullException(nameof(xtblRoot), "Invalid xtbl, missing [.root.Table] element");
            }

            MergeRecursive(edit.Xml.Wrap(), xtblRoot, edit.Action, false);

            var ms = new MemoryStream();
            disposables.Add(ms);
            gameXml.SerializeToMemoryStream(ms);
            file = file with
            {
                Content = ms,
                CompressedContent = null
            };
        }

        return file;
    }

    private static XmlDocument ReadXmlDocument(LogicalFile file)
    {
        // NOTE: StreamReader is important, it handles unicode properly
        using var reader = new StreamReader(file.Content);
        var gameXml = new XmlDocument { PreserveWhitespace = true };
        gameXml.Load(reader);
        return gameXml;
    }

    private void MergeRecursive(XmlNode source, XmlNode target, string? action, bool copyAttrs)
    {
        log.LogTrace("Merging [{src}] into [{dst}], action [{action}], copyAttrs [{copyAttrs}]", source.Name, target.Name, action, copyAttrs);
        if (copyAttrs)
        {
            xmlHelper.CopyAttrs(source, target);
            log.LogTrace("Copied attributes from [{src}] to [{dst}]", source.Name, target.Name);
        }

        if (!source.HasChildNodes)
        {
            log.LogTrace("Source [{src}] has no children, nothing to do", source.Name);
            return;
        }

        action ??= xmlHelper.GetListAction(source) ?? "add_new";
        var listAction = xmlHelper.ParseListAction(action);
        log.LogTrace("ListAction [{value}]", listAction);
        switch (listAction)
        {
            case ListAction.AddNew:
                foreach (XmlNode node in source.ChildNodes)
                {
                    AddNew(node, target);
                }

                break;
            case ListAction.Add:
                foreach (XmlNode node in source.ChildNodes)
                {
                    Add(node, target);
                }

                break;
            case ListAction.Replace:
                target.RemoveAll();
                foreach (XmlNode node in source.ChildNodes)
                {
                    Add(node, target);
                }

                break;
            case ListAction.CombineByField:
                var criteria = action["combine_by_field:".Length..];
                log.LogTrace("Combine criteria [{value}]", criteria);
                foreach (XmlNode node in source.ChildNodes)
                {
                    var matcher = xmlHelper.BuildSubnodeValueMatcher(criteria, node);
                    CopyFirstMatch(node, target, matcher);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Add node to target or reuse existing (by name), then descend
    /// </summary>
    private void AddNew(XmlNode node, XmlNode target)
    {
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                // use existing or create new
                var newTarget = target[node.LocalName] ?? XmlHelper.AppendNewChild(node.LocalName, target);
                log.LogTrace("MergeRecursive");
                MergeRecursive(node, newTarget, null, true);

                break;
            case XmlNodeType.Text:
                log.LogTrace("Set target text");
                xmlHelper.SetNodeXmlTextValue(target, node.InnerText);
                break;
        }
    }

    private void Add(XmlNode node, XmlNode target)
    {
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                var newChild = XmlHelper.AppendNewChild(node.LocalName, target);
                log.LogTrace("CopyRecursive");
                CopyRecursive(node, newChild, true);
                break;
            case XmlNodeType.Text:
                log.LogTrace("Set target text");
                xmlHelper.SetNodeXmlTextValue(target, node.InnerText);
                break;
        }
    }

    private void CopyRecursive(XmlNode node, XmlNode target, bool copyAttrs)
    {
        if (copyAttrs)
        {
            xmlHelper.CopyAttrs(node, target);
            log.LogTrace("Copied attributes from [{src}] to [{dst}]", node.Name, target.Name);
        }

        if (!node.HasChildNodes)
        {
            // TODO does this really work? any text is a TextNode too
            target.InnerText = node.InnerText;
            log.LogTrace("Node has no children, applied InnerText");
        }
        else
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                log.LogTrace("Add");
                Add(childNode, target);
            }
        }
    }

    private void CopyFirstMatch(XmlNode source, XmlNode target, IXmlNodeMatcher matcher)
    {
        // look for first match
        foreach (XmlNode x in target.ChildNodes)
        {
            if (source.LocalName == x.LocalName && matcher.DoesNodeMatch(x))
            {
                log.LogTrace("Found match, MergeRecursive");
                MergeRecursive(source, x, null, true);
                return;
            }
        }

        log.LogTrace("Nothing matched, fall back to Add");
        Add(source, target);
    }
}
