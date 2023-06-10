using System.Xml;

namespace SyncFaction.ModManager.Models;

public record XmlEditOperation(int Index, VppPath VppPath, string Action, List<XmlNode> Xml) : IOperation
{
    public ListAction ListAction { get; } = ParseListAction(Action);

    private static ListAction ParseListAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "add" => ListAction.Add,
            "replace" => ListAction.Replace,
            { } s when s.StartsWith("combine_by_field:") => ListAction.CombineByField,

            // TODO MM has logic for these actions but no mods use them:
            "combine_by_text" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            "combine_by_index" => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),
            { } s when s.StartsWith("combine_by_attribute:") => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action"),

            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action")
        };
    }
}