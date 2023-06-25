using System.Xml;

namespace SyncFaction.Core.Services.Xml;

internal class AlwaysFalseMatcher : IXmlNodeMatcher
{
    public bool DoesNodeMatch(XmlNode node) => false;
}
