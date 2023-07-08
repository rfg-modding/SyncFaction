using System.Xml;

namespace SyncFaction.ModManager.Services;

internal interface IXmlNodeMatcher
{
    bool DoesNodeMatch(XmlNode node);
}
