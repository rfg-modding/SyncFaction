using System.Xml;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.XmlModels;
using SyncFaction.Packer;

namespace SyncFaction.Core.Services.Xml;

// TODO move this to ModTools?
public class XmlMagic
{
    private readonly ILogger<XmlMagic> log;

    public XmlMagic(ILogger<XmlMagic> log) => this.log = log;

    public LogicalFile ApplyPatches(LogicalFile file, VppOperations vppOperations, List<IDisposable> disposables, CancellationToken token)
    {
        // NOTE: it's important to swap files first, then edit xml contents!
        var swaps = vppOperations.FileSwaps[file.Name];
        foreach (var swap in swaps)
        {
            log.LogDebug("swap [{key}] [{value}]", file.Name, swap.Target);
            var stream = swap.Target.OpenRead();
            disposables.Add(stream);
            file = file with
            {
                Content = stream,
                CompressedContent = null
            };
        }

        var edits = vppOperations.XmlEdits[file.Name];
        foreach (var edit in edits)
        {
            var ext = file.Name.Split(".").Last().ToLowerInvariant();
            if (!XmlHelper.KnownXmlExtensions.Contains(ext))
            {
                var extList = string.Join(", ", XmlHelper.KnownXmlExtensions);
                throw new InvalidOperationException($"Can not edit file [{file.Name}]. Supported file extensions: [{extList}]");
            }

            log.LogDebug("edit [{key}] [{value}]", file.Name, edit.Action);
            // NOTE: StreamReader is important, it handles unicode properly
            using var reader = new StreamReader(file.Content);
            var gameXml = new XmlDocument();
            gameXml.PreserveWhitespace = true;
            gameXml.Load(reader);
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

    /// <summary>
    /// Add node to target or reuse existing (by name), then descend
    /// </summary>
    public static void AddNew(XmlNode node, XmlNode target)
    {
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                // use existing or create new
                var newTarget = target[node.LocalName] ?? XmlHelper.AppendNewChild(node.LocalName, target);
                MergeRecursive(node, newTarget, null, true);

                break;
            case XmlNodeType.Text:
                XmlHelper.SetNodeXmlTextValue(target, node.InnerText);
                break;
        }
    }

    public static void CopyRecursive(XmlNode node, XmlNode target, bool copyAttrs)
    {
        if (copyAttrs)
        {
            XmlHelper.CopyAttrs(node, target);
        }

        if (!node.HasChildNodes)
        {
            // TODO does this really work? any text is a TextNode too
            target.InnerText = node.InnerText;
        }
        else
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                Add(childNode, target);
            }
        }
    }

    private static void MergeRecursive(XmlNode source, XmlNode target, string? action, bool copyAttrs)
    {
        //todo walk xml

        if (copyAttrs)
        {
            XmlHelper.CopyAttrs(source, target);
        }

        if (!source.HasChildNodes)
        {
            return;
        }

        action ??= XmlHelper.GetListAction(source) ?? "add_new";
        var listAction = XmlHelper.ParseListAction(action);
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
                var criteria = action.Substring("combine_by_field:".Length);
                foreach (XmlNode node in source.ChildNodes)
                {
                    var matcher = XmlHelper.BuildSubnodeValueMatcher(criteria, node);
                    CopyFirstMatch(node, target, matcher);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void Add(XmlNode node, XmlNode target)
    {
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                var newChild = XmlHelper.AppendNewChild(node.LocalName, target);
                CopyRecursive(node, newChild, true);
                break;
            case XmlNodeType.Text:
                XmlHelper.SetNodeXmlTextValue(target, node.InnerText);
                break;
        }
    }

    private static void CopyFirstMatch(XmlNode source, XmlNode target, IXmlNodeMatcher matcher)
    {
        // look for first match
        foreach (XmlNode x in target.ChildNodes)
        {
            if (source.LocalName == x.LocalName && matcher.DoesNodeMatch(x))
            {
                MergeRecursive(source, x, null, true);
                return;
            }
        }

        // if nothing matched, fallback to add
        Add(source, target);
    }
}
