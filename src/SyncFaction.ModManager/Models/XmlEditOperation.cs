using System.Xml;

namespace SyncFaction.ModManager.Models;

public record XmlEditOperation(int Index, VppPath VppPath, string Action, List<XmlNode> Xml) : IOperation;
