using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class Edit : HasNestedXml, IChange
{
    [XmlAttribute]
    public string File { get; set; }

    [XmlAttribute]
    public string LIST_ACTION { get; set; }

    public void ApplyUserInput(Dictionary<string, XmlNode> selectedValues)
    {
	    if (NestedXml.Count == 0)
	    {
		    // nothing can be replaced
		    return;
	    }

	    var holder = NestedXml.Wrap();

	    InsertUserEditValuesRecursive(holder, selectedValues);

	    RemoveUserEditNodesRecursive(holder);
    }

	/// <summary>
	/// Append to all nested "user_input" tags corresponding selected values
	/// </summary>
    private static void InsertUserEditValuesRecursive(XmlNode node, Dictionary<string, XmlNode> selectedValues)
	{
		if (node is not XmlElement element)
		{
			return;
		}

		var nameLower = element.Name.ToLower();
		if (nameLower == UserInputName)
		{
			var key = element.InnerText.ToLowerInvariant();
			// TODO support null (no-op)
			var replacementHolder = selectedValues[key];
			var parent = element.ParentNode;
			XmlNode current = element;
			for (var i = 0; i< replacementHolder.ChildNodes.Count; i++)
			{
				var replacement = replacementHolder.ChildNodes[i];
				parent.InsertAfter(replacement, current);
				current = replacement;
				// now outer loop will iterate over newly inserted replacement results. recursive edits are possible yaay!
			}
		}
		else if (element.HasChildNodes)
		{
			// descend
			// can't use for/foreach: we modify collection in-place!
			var i = 0;
			while (i < element.ChildNodes.Count)
			{
				var nextNode = element.ChildNodes[i];
				InsertUserEditValuesRecursive(nextNode, selectedValues);

				i++;
			}
		}
	}

	/// <summary>
	/// Remove "user_input" nodes after all edits are done. Returns true if node was removed (outer loop SHOULD NOT advance)
	/// </summary>
	private static bool RemoveUserEditNodesRecursive(XmlNode node)
	{
		if (node is not XmlElement element)
		{
			return false;
		}

		if (element.Name.Equals(UserInputName, StringComparison.InvariantCultureIgnoreCase))
		{
			var parent = element.ParentNode;
			parent.RemoveChild(element);
			return true;
		}

		if (element.HasChildNodes)
		{
			// descend
			// can't use for/foreach: we modify collection in-place!
			var i = 0;
			while (i < element.ChildNodes.Count)
			{
				var nextNode = element.ChildNodes[i];
				var removed = RemoveUserEditNodesRecursive(nextNode);
				if (!removed)
				{
					i++;
				}
			}
		}

		return false;
	}

	private static readonly string UserInputName = "user_input";

}
