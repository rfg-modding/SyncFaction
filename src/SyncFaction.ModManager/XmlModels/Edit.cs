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
        NestedXml.Clear();
        foreach (var x in holder.ChildNodes)
        {
            NestedXml.Add((XmlNode)x);
        }
    }

    public IChange Clone()
    {
        return new Edit()
        {
            File = File,
            LIST_ACTION = LIST_ACTION,
            NestedXml = NestedXml.Select(x => x.Clone()).ToList()
        };
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

		if (element.Name.Equals(UserInputName, StringComparison.InvariantCultureIgnoreCase))
		{
			var key = element.InnerText.ToLowerInvariant();
			var replacementHolder = selectedValues[key];
            if (replacementHolder.OwnerDocument != element.OwnerDocument)
            {
                replacementHolder = element.OwnerDocument.ImportNode(replacementHolder, true);
            }
			var parent = element.ParentNode;
			XmlNode current = element;
            // collection of child nodes shrinks because we actually move elements to another place
            while (replacementHolder.ChildNodes.Count > 0)
			{
				var replacement = replacementHolder.ChildNodes[0];
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
			// descend. can't use for/foreach: we modify collection in-place!
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
