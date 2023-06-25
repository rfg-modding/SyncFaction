using System.Collections.Immutable;
using System.Xml;
using SyncFaction.ModManager.Models;

namespace SyncFaction.Core.Services.Xml;

public static class XmlHelper
{
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

    public static void ImportNodes(List<XmlNode> nodes, XmlElement targetParent)
    {
        foreach (var node in nodes)
        {
            var imported = targetParent.OwnerDocument.ImportNode(node, true);
            targetParent.AppendChild(imported);
        }
    }

    public static string FormatXPath(string value)
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
        return string.Join("/", tags);
    }

    public static ListAction ParseListAction(string action) =>
        action.ToLowerInvariant() switch
        {
            "add_new" => ListAction.AddNew,
            "add" => ListAction.Add,
            "replace" => ListAction.Replace,
            {
            } s when s.StartsWith("combine_by_field:", StringComparison.OrdinalIgnoreCase) => ListAction.CombineByField,

            // TODO MM has logic for these actions but no mods use them:
            "combine_by_text" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            "combine_by_index" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            {
            } s when s.StartsWith("combine_by_attribute:", StringComparison.OrdinalIgnoreCase) => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),

            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action")
        };

    public static XmlText GetNodeOrSubnodeText(XmlNode node)
    {
        if (node.NodeType == XmlNodeType.Text)
        {
            return (XmlText) node;
        }

        foreach (XmlNode childNode in node.ChildNodes)
        {
            if (childNode.NodeType == XmlNodeType.Text)
            {
                return (XmlText) childNode;
            }
        }

        return null;
    }

    public static void SetNodeXmlTextValue(XmlNode node, string textString)
    {
        var xmlText = GetNodeOrSubnodeText(node);

        if (string.IsNullOrEmpty(textString))
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
                var textNode = node.OwnerDocument.CreateTextNode(textString);
                node.AppendChild(textNode);
            }
        }
    }

    public static void CopyAttrs(XmlNode source, XmlNode target)
    {
        if (source.Attributes is null)
        {
            var breakMe = 0;
        }

        foreach (XmlAttribute attribute in source.Attributes)
        {
            if (!attribute.LocalName.Equals(ListActionAttribute, StringComparison.OrdinalIgnoreCase))
            {
                var newAttr = target.OwnerDocument.CreateAttribute(attribute.LocalName);
                newAttr.Value = attribute.InnerText;
                target.Attributes.SetNamedItem(newAttr);
            }
        }
    }

    public static string? GetListAction(XmlNode node)
    {
        foreach (XmlAttribute attribute in node.Attributes)
        {
            if (attribute.LocalName.Equals(ListActionAttribute, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(attribute.InnerText))
            {
                return attribute.InnerText;
            }
        }

        return null;
    }

    public static XmlNode AppendNewChild(string name, XmlNode target) => target.AppendChild(target.OwnerDocument!.CreateElement(name))!;

    internal static IXmlNodeMatcher BuildSubnodeValueMatcher(string criteria, XmlNode editChild)
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

    public static readonly ImmutableHashSet<string> KnownXmlExtensions = new HashSet<string>
    {
        "xtbl",
        "dtdox",
        "gtodx"
    }.ToImmutableHashSet();

    public const string ListActionAttribute = "list_action";
}
