using System.Xml;

namespace SyncFaction.Core.Services.Xml;

interface IXmlNodeMatcher
{
    bool DoesNodeMatch(XmlNode node);
}