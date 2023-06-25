using System.Xml;

namespace SyncFaction.Core.Services.Xml;

internal class AndMatcher : IXmlNodeMatcher
{
    public IXmlNodeMatcher Matcher0;
    public IXmlNodeMatcher Matcher1;

    public AndMatcher(IXmlNodeMatcher matcher0, IXmlNodeMatcher matcher1)
    {
        Matcher0 = matcher0;
        Matcher1 = matcher1;
    }

    public bool DoesNodeMatch(XmlNode node) => Matcher0.DoesNodeMatch(node) && Matcher1.DoesNodeMatch(node);

    public virtual string ToString() => Matcher0 + " AND " + Matcher1;
}
