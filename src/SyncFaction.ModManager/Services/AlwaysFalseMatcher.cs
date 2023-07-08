using System.Xml;

namespace SyncFaction.ModManager.Services;

internal class AlwaysFalseMatcher : IXmlNodeMatcher
{
    public bool DoesNodeMatch(XmlNode node) => false;
}
