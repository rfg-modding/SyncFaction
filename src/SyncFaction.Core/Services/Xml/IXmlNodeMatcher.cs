using System.Xml;

namespace SyncFaction.Core.Services.Xml;

internal interface IXmlNodeMatcher
{
    bool DoesNodeMatch(XmlNode node);
}
