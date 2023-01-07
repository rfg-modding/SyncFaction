using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
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


	public ModInfo LoadFromXml(Stream stream)
	{
		using var reader = XmlReader.Create(stream, Settings);
		var serializer = new XmlSerializer(typeof(ModInfo));
		return (ModInfo) serializer.Deserialize(reader)!;
	}

	public void ApplyUserInput(ModInfo modInfo)
	{
		var selectedValues = modInfo.UserInput.ToDictionary(x => x.Name.ToLowerInvariant(), x => x.SelectedValue);
		foreach (var change in modInfo.Changes)
		{
			change.ApplyUserInput(selectedValues);
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

}

public class Settings
{
    public Dictionary<long, Mod> Mods { get; set; } = new();

    public class Mod
    {
        public Dictionary<string, ListBox> ListBoxes { get; set; } = new();
    }

    public class ListBox
    {
        public int SelectedIndex { get; set; }
        public string? CustomValue { get; set; }
    }

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


*/
