using System.Xml;

namespace SyncFaction.Core.Services.Xml;

class AlwaysFalseMatcher : IXmlNodeMatcher
{
    public bool DoesNodeMatch(XmlNode node) => false;
}