using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;
using SyncFaction.ModManager.XmlModels;

namespace SyncFaction.ModManager;

public class ModTools
{
    private readonly ILogger<ModTools> log;

    public ModTools(ILogger<ModTools> log)
    {
        this.log = log;
    }

    private static readonly XmlReaderSettings Settings = new()
    {
		IgnoreComments = true,
		IgnoreWhitespace = true,
		IgnoreProcessingInstructions = true,
	};


	public ModInfo LoadFromXml(Stream stream, IDirectoryInfo xmlFileDirectory)
	{
		using var reader = XmlReader.Create(stream, Settings);
		var serializer = new XmlSerializer(typeof(ModInfo));
        var modInfo = (ModInfo) serializer.Deserialize(reader)!;
        modInfo.WorkDir = xmlFileDirectory;
        return modInfo;
	}

    /// <summary>
    /// Three stages:
    /// <para>replace user_input placeholders;</para>
    /// <para>substitute FileUserInputs;</para>
    /// <para>mirror edits between misc and table</para>
    /// </summary>
    public void ApplyUserInput(ModInfo modInfo)
    {
        if (modInfo.Changes.NestedXml.Count == 0)
        {
            // nothing can be replaced
            return;
        }

        var typedChanges = modInfo.Changes.NestedXml.Wrap(nameof(TypedChangesHolder.TypedChanges)).Clone();
        var selectedValues = modInfo.UserInput.ToDictionary(x => x.Name.ToLowerInvariant(), x => x.SelectedValue.Clone());
        InsertUserEditValuesRecursive(typedChanges, selectedValues);
        RemoveUserEditNodesRecursive(typedChanges);

        var holder = typedChanges.Wrap();
        var serializer = new XmlSerializer(typeof(TypedChangesHolder));
        using var reader = new XmlNodeReader(holder);
        var typedChangesHolder = (TypedChangesHolder) serializer.Deserialize(reader);

        foreach (var replace in typedChangesHolder.TypedChanges.OfType<Replace>())
        {
            ApplyReplaceUserInput(replace, selectedValues);
        }

        var mirroredChanges = MirrorMiscTableChanges(modInfo.WorkDir.FileSystem, typedChangesHolder.TypedChanges);

        modInfo.TypedChanges = typedChangesHolder.TypedChanges.Concat(mirroredChanges).ToList();
    }

    /// <summary>
    /// For every edit of misc.vpp or table.vpp, add similar edits for second file. Only for XTBL edits/replacements!
    /// </summary>
    private IEnumerable<IChange> MirrorMiscTableChanges(IFileSystem fs, IReadOnlyList<IChange> changes)
    {
        foreach (var change in changes)
        {
            var vppPaths = GetPaths(fs, change.File);
            if (!vppPaths.File.EndsWith(".xtbl"))
            {
                continue;
            }

            var mirror = vppPaths.Archive.Replace('\\', '/') switch
            {
                "data/misc.vpp_pc" => "data/table.vpp_pc",
                "data/table.vpp_pc" => "data/misc.vpp_pc",
                _ => null
            };
            if (mirror is null)
            {
                continue;
            }

            var mirrorChange = change.Clone();
            mirrorChange.File = fs.Path.Combine(mirror, vppPaths.File);
            yield return mirrorChange;
        }

    }

    public void ApplyReplaceUserInput(Replace replace, Dictionary<string, XmlNode> selectedValues)
    {
        if (!string.IsNullOrEmpty(replace.FileUserInput))
        {
            if (!string.IsNullOrEmpty(replace.File))
            {
                throw new ArgumentException($"Both {nameof(replace.NewFile)} and {nameof(replace.NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{replace.NewFile}], [{replace.NewFileUserInput}]");
            }

            // TODO support null (no-op)
            var holder = selectedValues[replace.FileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            replace.File = holder.ChildNodes[0].InnerText;
        }

        if (!string.IsNullOrEmpty(replace.NewFileUserInput))
        {
            if (!string.IsNullOrEmpty(replace.NewFile))
            {
                throw new ArgumentException($"Both {nameof(replace.NewFile)} and {nameof(replace.NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{replace.NewFile}], [{replace.NewFileUserInput}]");
            }
            // TODO support null (no-op)
            var holder = selectedValues[replace.NewFileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            replace.NewFile = holder.ChildNodes[0].InnerText;
        }
    }

    public void CopySameOptions(ModInfo modInfo)
    {
        var listBoxes = modInfo.UserInput.OfType<ListBox>().ToDictionary(x => x.Name.ToLowerInvariant());
        // set copies for listboxes which reference others
        foreach (var listBox in listBoxes.Values)
        {
            if (string.IsNullOrWhiteSpace(listBox.SameOptionsAs))
            {
                continue;
            }

            var source = listBoxes[listBox.SameOptionsAs.ToLowerInvariant()];
            // NOTE what could possibly go wrong if i don't do a deep copy? options are not modified anywhere. also, custom option is separate
            listBox.XmlOptions = source.XmlOptions;
        }

    }

    public Settings.Mod SaveCurrentSettings(ModInfo modInfo)
    {
        var result = new Settings.Mod();
        var listBoxes = modInfo.UserInput.OfType<ListBox>().ToDictionary(x => x.Name.ToLowerInvariant());
        foreach (var kv in listBoxes)
        {
            result.ListBoxes[kv.Key] = new Settings.ListBox()
            {
                CustomValue = kv.Value.DisplayOptions.OfType<CustomOption>().FirstOrDefault()?.Value,
                SelectedIndex = kv.Value.SelectedIndex
            };
        }

        return result;
    }

    public void LoadSettings(Settings.Mod settings, ModInfo modInfo)
    {
        var listBoxes = modInfo.UserInput.OfType<ListBox>().ToDictionary(x => x.Name.ToLowerInvariant());
        foreach (var kv in settings.ListBoxes)
        {
            var listBox = listBoxes[kv.Key];
            listBox.SelectedIndex = kv.Value.SelectedIndex;
            var customOption = listBox.DisplayOptions.OfType<CustomOption>().FirstOrDefault();
            if (customOption is not null)
            {
                customOption.Value = kv.Value.CustomValue;
            }
        }
    }

    private static string SanitizePath(string path)
    {
        return path
                .Replace("//", "")
                .Replace("..", "")
                .Replace(":", "")
                .ToLowerInvariant()
            ;
    }

    public ModInfoOperations BuildOperations(ModInfo modInfo)
    {
        // ApplyUserInput is expected to be called prior to this. We expect actual state here
        // not using FileUserInput because they are copied to File already, same with NewFile

        // map relative paths "foo/bar.xtbl" to FileInfo
        var fs = modInfo.WorkDir.FileSystem;
        var relativeModFiles = modInfo.WorkDir
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .ToImmutableDictionary(x => fs.Path.GetRelativePath(modInfo.WorkDir.FullName, x.FullName.ToLowerInvariant()));

        // map replacements inside vpp to relative paths
        var references = modInfo.TypedChanges
            .OfType<Replace>()
            .Select(x => new {vppPath = GetPaths(fs, x.File), target = SanitizePath(x.NewFile)})
            .Where(x => !string.IsNullOrWhiteSpace(x.target))
            .ToImmutableDictionary(x => x.vppPath, x => x.target);

        var referencedFiles = references
            .Select(x => new {vppPath = x.Key, target = x.Value, fileInfo = relativeModFiles!.GetValueOrDefault(x.Value, null)})
            .ToList();
        var missing = referencedFiles
            .Where(x => x.fileInfo is null)
            .ToList();
        if (missing.Any())
        {
            var files = string.Join(", ", missing.Select(x => x.target));
            throw new ArgumentException($"ModInfo references {missing.Count} nonexistent files: [{files}]");
        }

        var referencedFilesDict = referencedFiles.ToImmutableDictionary(x => x.vppPath, x => x.fileInfo!);
        var replaceOperations = modInfo.TypedChanges
            .OfType<Replace>()
            .Select((x, i) => ConvertToOperation(x, i, fs, referencedFilesDict))
            .ToList();
        var editOperations = modInfo.TypedChanges
            .OfType<Edit>()
            .Select((x, i) => ConvertToOperation(x, i, fs))
            .ToList();
        return new ModInfoOperations(replaceOperations, editOperations);
    }

    private FileSwapOperation ConvertToOperation(Replace replace, int index, IFileSystem fs, IImmutableDictionary<VppPath, IFileInfo> referencedFiles)
    {
        return new FileSwapOperation(index, GetPaths(fs, replace.File), referencedFiles[GetPaths(fs, replace.File)]);
    }

    private XmlEditOperation ConvertToOperation(Edit edit, int index, IFileSystem fs)
    {
        return new XmlEditOperation(index, GetPaths(fs, edit.File), edit.LIST_ACTION, edit.NestedXml.ToList());
    }

    /// <summary>
    /// Check, sanitize and split path from Change to "data/vpp" + "relative/file/path"
    /// </summary>
    private VppPath GetPaths(IFileSystem fs, string rawPath)
    {
        var path = SanitizePath(rawPath.ToLowerInvariant());
        var parts = path.Split('\\', '/');
        if (parts.Length < 3)
        {
            throw new ArgumentException($"modinfo.xml references wrong vpp to edit: [{path}]. path should be 'data\\something.vpp_pc\\...'");
        }

        // NOTE: legacy mods for Steam edition can be used if we patch vpp location "build/pc/cache/foo.vpp" to "data/foo.vpp"
        if (string.Join("/", parts[..3]) == "build/pc/cache")
        {
            parts = new[] {"data"}.Concat(parts[3..]).ToArray();
        }

        var data = parts[0];
        if (data != "data")
        {
            throw new ArgumentException($"modinfo.xml references wrong vpp to edit: [{path}]. path should start with 'data'");
        }

        var vpp = parts[1];
        if (vpp.EndsWith(".vpp"))
        {
            vpp += "_pc";
        }
        if (!vpp.EndsWith(".vpp_pc"))
        {
            throw new ArgumentException($"modinfo.xml references wrong vpp to edit: [{path}]. path should reference vpp_pc archive");
        }

        var dataVpp = fs.Path.Combine(data, vpp);
        var archiveRelativePath = fs.Path.Combine(parts[2..]);

        return new (dataVpp, archiveRelativePath);
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
			var replacementHolder = selectedValues[key].Clone();
            if (replacementHolder.OwnerDocument != element.OwnerDocument)
            {
                // cloning every time because nodes can belong to same document and are moved to different places
                replacementHolder = element.OwnerDocument.ImportNode(replacementHolder, true).Clone();
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



/*

    TODO support no-op at merge time


	ModInfo.xml
		Mod
			Changes
				<Replace File="build/pc/cache/items.vpp/repair_tool.str2_pc" NewFile="repair_tool.str2_pc" />
					replaces a file, no magic

				<Edit File="">XML</Edit>
					"Content of tags with the same name will be replaced with the new content"
					if a node has multiple children, LIST_ACTION is used
						ADD							add to child nodes
						REPLACE						replace al child nodes
						COMBINE_BY_TEXT				?
						COMBINE_BY_INDEX			?
						COMBINE_BY_FIELD:X,Y,A\B\C	replaces child nodes matching tags X or Y or looking inside nested tags A->B->C
						COMBINE_BY_ATTRIBUTE:X,Y	replaces child nodes matching attributes X or Y


						COMBINE_BY_FIELD:X
							iterates through all children of the list in search for a child that has equal values
							If such a child is found, the content of the tags will be merged
							If no child with equal values is found, the tag will be added as a new one
			<USER_INPUT>Tank Camera Distance</USER_INPUT> - is replaced by contents

			example from steam guide:
			<Edit file="build/pc/cache/misc.vpp/weapons.xtbl" LIST_ACTION="COMBINE_BY_FIELD:Name,_Editor\Category">
				<Weapon>
					<Name>edf_pistol</Name>
					<_Editor><Category>Entries:EDF</Category></_Editor>
					<Max_Rounds>9999</Max_Rounds>
				</Weapon>
			</Edit>

	IMPROVEMENTS
		* user inputs
			checkbox
			textbox (maybe optional eg when certain selectbox value is selected)
		* support <replace file> tag from inputs (now only <edit> is supported)
		* <add file> tag




    in 139 mods from FF, Nexus and ModDb, i have found these LIST_ACTIONs in modinfo.xml:
        ADD
        COMBINE_BY_FIELD:Name
        COMBINE_BY_FIELD:Name,Unique_ID
        COMBINE_BY_FIELD:Name,_Editor\Category
        REPLACE
        nothing specified == CopyNodeToTargetIfNeeded

    NOTES
        * MM uses xtbl.root.Table as starting element
        * default LIST_ACTION is ADD
        * MM copies attributes, except for root level
        *


*/
