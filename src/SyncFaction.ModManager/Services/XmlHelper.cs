using System.Collections.Immutable;
using System.Xml;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;

namespace SyncFaction.ModManager.Services;

public class XmlHelper
{
    private readonly ILogger<XmlHelper> log;

    public XmlHelper(ILogger<XmlHelper> log)
    {
        this.log = log;
    }

    internal static XmlNode? GetSubnodeByXPath(XmlNode? node, string xPath) => string.IsNullOrWhiteSpace(xPath)
        ? null
        : node?.SelectSingleNode(xPath);

    private string FormatXPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(nameof(value));
        }

        var tags = value.Split(new[]
            {
                '\\'
            },
            StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("/", tags);
        log.LogTrace("Formatted [{value}] to xpath [{xpath}]", value, result);
        return result;
    }

    internal ListAction ParseListAction(string action) =>
        action.ToLowerInvariant() switch
        {
            "add_new" => ListAction.AddNew,
            "add" => ListAction.Add,
            "replace" => ListAction.Replace,
            {
            } s when s.StartsWith("combine_by_field:", StringComparison.OrdinalIgnoreCase) => ListAction.CombineByField,

            // NOTE: MM has logic for these actions but no mods use them:
            "combine_by_text" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            "combine_by_index" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            {
            } s when s.StartsWith("combine_by_attribute:", StringComparison.OrdinalIgnoreCase) => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),

            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action")
        };

    private XmlText? GetNodeOrSubnodeText(XmlNode node)
    {
        if (node.NodeType == XmlNodeType.Text)
        {
            log.LogTrace("Node is Text");
            return (XmlText) node;
        }

        foreach (XmlNode childNode in node.ChildNodes)
        {
            if (childNode.NodeType == XmlNodeType.Text)
            {
                log.LogTrace("Found child node of type Text");
                return (XmlText) childNode;
            }
        }

        return null;
    }

    internal void SetNodeXmlTextValue(XmlNode node, string value)
    {
        var xmlText = GetNodeOrSubnodeText(node);
        if (string.IsNullOrEmpty(value))
        {
            if (xmlText is not null)
            {
                //If string empty and xmlText isn't null, delete xmlText node
                node.RemoveChild(xmlText);
                log.LogTrace("Removed existing Text node");
            }

            // else, do nothing and return
            log.LogTrace("Xml text and value are empty, nothing to do");
        }
        else
        {
            if (xmlText is not null)
            {
                //If string not empty and xmlText != null, set it's value to textString
                xmlText.InnerText = value;
                log.LogTrace("Applied text value to InnerText");
            }
            else
            {
                //If xmlText is null then add XmlText subnode with the value of textString
                var textNode = node.OwnerDocument!.CreateTextNode(value);
                node.AppendChild(textNode);
                log.LogTrace("Added Text node");
            }
        }
    }

    internal void CopyAttrs(XmlNode source, XmlNode target)
    {
        foreach (XmlAttribute attribute in source.Attributes!)
        {
            if (!attribute.LocalName.Equals(ListActionAttribute, StringComparison.OrdinalIgnoreCase))
            {
                var newAttr = target.OwnerDocument!.CreateAttribute(attribute.LocalName);
                newAttr.Value = attribute.InnerText;
                target.Attributes!.SetNamedItem(newAttr);
            }
        }
    }

    internal string? GetListAction(XmlNode node)
    {
        foreach (XmlAttribute attribute in node.Attributes!)
        {
            if (attribute.LocalName.Equals(ListActionAttribute, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(attribute.InnerText))
            {
                var value = attribute.InnerText;
                log.LogTrace("Found ListAction attribute with value [{value}]", value);
                return value;
            }
        }

        if (node.LocalName.Equals(XmlModels.Extensions.HolderName, StringComparison.OrdinalIgnoreCase))
        {
            // top-level <edit> without list_action means "add"
            return "add";
        }

        return null;
    }

    internal static XmlNode AppendNewChild(string name, XmlNode target) => target.AppendChild(target.OwnerDocument!.CreateElement(name))!;

    internal IXmlNodeMatcher BuildSubnodeValueMatcher(string criteria, XmlNode editChild)
    {
        if (string.IsNullOrWhiteSpace(criteria))
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        //example: "Name,_Editor\Category"
        var xPaths = criteria.Split(',').Select(FormatXPath).ToList();
        var firstXpath = xPaths.First();
        var value = GetSubnodeByXPath(editChild, firstXpath)?.InnerText;
        if (string.IsNullOrWhiteSpace(value))
        {
            // NOTE: if first is always false, ignore others because they are chained with AND
            log.LogTrace("Always false matcher");
            return new AlwaysFalseMatcher();
        }

        IXmlNodeMatcher matcher = new SubnodeValueMatcher(firstXpath, value);
        foreach (var x in xPaths.Skip(1))
        {
            var result = GetSubnodeByXPath(editChild, x)?.InnerText ?? string.Empty;
            matcher = new AndMatcher(matcher, new SubnodeValueMatcher(x, result));
        }

        return matcher;
    }

    public ImmutableHashSet<string> KnownXmlExtensions { get; } = new HashSet<string>
    {
        "xtbl",
        "dtdox",
        "gtodx"
    }.ToImmutableHashSet();

    private const string ListActionAttribute = "list_action";
}
