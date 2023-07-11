using System.Xml;

namespace SyncFaction.ModManager.Services;

internal class SubnodeValueMatcher : IXmlNodeMatcher
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

    public bool DoesNodeMatch(XmlNode node) => XmlHelper.GetSubnodeByXPath(node, XPath)?.InnerText == ExpectedValue;

    public virtual string ToString() => XPath + "=" + ExpectedValue;
}
