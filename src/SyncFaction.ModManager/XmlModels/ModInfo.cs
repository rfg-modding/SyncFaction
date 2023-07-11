using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using SyncFaction.ModManager.Models;

namespace SyncFaction.ModManager.XmlModels;

[XmlRoot("Mod")]
public class ModInfo
{
    [XmlIgnore]
    public IDirectoryInfo WorkDir { get; set; } = null!;

    [XmlAttribute]
    public string Name { get; set; } = null!;

    public string Author { get; set; } = null!;

    public string Description { get; set; } = null!;

    public WebLink WebLink { get; set; } = null!;

    [XmlArrayItem(typeof(ListBox))]
    public List<Input> UserInput { get; set; } = null!;

    public Changes Changes { get; set; } = null!;

    [XmlIgnore]
    public IReadOnlyList<IChange> TypedChanges { get; set; } = null!;

    [SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Why not?")]
    private ILogger log = null!;

    protected void Init(IDirectoryInfo xmlFileDirectory, ILogger logger)
    {
        WorkDir = xmlFileDirectory;
        log = logger;
    }

    public static ModInfo LoadFromXml(Stream stream, IDirectoryInfo xmlFileDirectory, ILogger log)
    {
        using var reader = XmlReader.Create(stream, Settings);
        var serializer = new XmlSerializer(typeof(ModInfo));
        var modInfo = (ModInfo) serializer.Deserialize(reader)!;
        modInfo.Init(xmlFileDirectory, log);
        return modInfo;
    }

    private static readonly XmlReaderSettings Settings = new()
    {
        IgnoreComments = true,
        IgnoreWhitespace = true,
        IgnoreProcessingInstructions = true
    };

    /// <summary>
    /// Three stages:
    /// <para>replace user_input placeholders;</para>
    /// <para>substitute FileUserInputs;</para>
    /// <para>mirror edits between misc and table</para>
    /// </summary>
    public void ApplyUserInput()
    {
        log.LogTrace("Applying user input");
        if (Changes.NestedXml.Count == 0)
        {
            // nothing can be replaced
            log.LogTrace("ModInfo has no Changes");
            return;
        }

        var typedChanges = Changes.NestedXml.Wrap(nameof(TypedChangesHolder.TypedChanges)).Clone();
        var selectedValues = UserInput.ToDictionary(static x => x.Name.ToLowerInvariant(), static x => x.SelectedValue.Clone());
        InsertUserEditValuesRecursive(typedChanges, selectedValues);
        RemoveUserEditNodesRecursive(typedChanges);
        var holder = typedChanges.Wrap();
        var serializer = new XmlSerializer(typeof(TypedChangesHolder));
        using var reader = new XmlNodeReader(holder);
        var typedChangesHolder = (TypedChangesHolder) serializer.Deserialize(reader)!;
        foreach (var replace in typedChangesHolder.TypedChanges.OfType<Replace>())
        {
            ApplyReplaceUserInput(replace, selectedValues);
        }

        var mirroredChanges = MirrorMiscTableChanges(WorkDir.FileSystem, typedChangesHolder.TypedChanges);
        TypedChanges = typedChangesHolder.TypedChanges.Concat(mirroredChanges).ToList();
    }

    private void ApplyReplaceUserInput(Replace replace, Dictionary<string, XmlNode> selectedValues)
    {
        if (!string.IsNullOrEmpty(replace.FileUserInput))
        {
            if (!string.IsNullOrEmpty(replace.File))
            {
                throw new ArgumentException($"Both {nameof(replace.NewFile)} and {nameof(replace.NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{replace.NewFile}], [{replace.NewFileUserInput}]");
            }

            var holder = selectedValues[replace.FileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            replace.File = holder.ChildNodes[0]!.InnerText;
            log.LogTrace("File Replace [{name}] set to value [{value}]", replace.FileUserInput, replace.File);
        }

        if (!string.IsNullOrEmpty(replace.NewFileUserInput))
        {
            if (!string.IsNullOrEmpty(replace.NewFile))
            {
                throw new ArgumentException($"Both {nameof(replace.NewFile)} and {nameof(replace.NewFileUserInput)} attributes are not allowed together. Erase one of values to fix: [{replace.NewFile}], [{replace.NewFileUserInput}]");
            }

            var holder = selectedValues[replace.NewFileUserInput];
            if (holder.ChildNodes.Count != 1)
            {
                throw new ArgumentException($"File manipulations require option to have exactly one string value. Selected option: [{holder.InnerXml}]");
            }

            replace.NewFile = holder.ChildNodes[0]!.InnerText;
            log.LogTrace("NewFile Replace [{name}] set to value [{value}]", replace.NewFileUserInput, replace.NewFile);
        }
    }

    public void CopySameOptions()
    {
        var listBoxes = UserInput.OfType<ListBox>().ToDictionary(x => x.Name.ToLowerInvariant());
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

    public Settings.Mod SaveCurrentSettings()
    {
        var result = new Settings.Mod();
        var listBoxes = UserInput.OfType<ListBox>().ToDictionary(static x => x.Name.ToLowerInvariant());
        foreach (var kv in listBoxes)
        {
            result.ListBoxes[kv.Key] = new Settings.ListBox
            {
                CustomValue = kv.Value.DisplayOptions.OfType<CustomOption>().FirstOrDefault()?.Value,
                SelectedIndex = kv.Value.SelectedIndex
            };
        }

        return result;
    }

    public void LoadSettings(Settings.Mod settings)
    {
        var listBoxes = UserInput.OfType<ListBox>().ToDictionary(x => x.Name.ToLowerInvariant());
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

    public ModInfoOperations BuildOperations()
    {
        // ApplyUserInput is expected to be called prior to this. We expect actual state here
        // not using FileUserInput because they are copied to File already, same with NewFile

        // map relative paths "foo/bar.xtbl" to FileInfo
        var fs = WorkDir.FileSystem;
        var relativeModFiles = WorkDir.EnumerateFiles("*", SearchOption.AllDirectories).ToImmutableDictionary(x => fs.Path.GetRelativePath(WorkDir.FullName, x.FullName.ToLowerInvariant()));

        // map replacements inside vpp to relative paths
        var references = TypedChanges.OfType<Replace>()
            .Select(x => new
            {
                vppPath = GetPaths(fs, x.File),
                target = SanitizePath(x.NewFile)
            })
            .Where(static x => !string.IsNullOrWhiteSpace(x.target))
            .ToImmutableDictionary(static x => x.vppPath, static x => x.target);

        var referencedFiles = references.Select(x => new
            {
                vppPath = x.Key,
                target = x.Value,
                fileInfo = relativeModFiles!.GetValueOrDefault(x.Value, null)
            })
            .ToList();
        log.LogTrace("Modinfo.xml file references: [{files}]", string.Join(", ", referencedFiles.Select(static x =>$"({x.vppPath} => {x.fileInfo?.FullName})")));
        var missing = referencedFiles.Where(static x => x.fileInfo is null).ToList();
        if (missing.Any())
        {
            var files = string.Join(", ", missing.Select(static x => x.target));
            throw new ArgumentException($"ModInfo references {missing.Count} nonexistent files: [{files}]");
        }

        var referencedFilesDict = referencedFiles.ToImmutableDictionary(static x => x.vppPath, static x => x.fileInfo!);
        var replaceOperations = TypedChanges.OfType<Replace>().Select((x, i) => ConvertToOperation(x, i, fs, referencedFilesDict)).ToList();
        var editOperations = TypedChanges.OfType<Edit>().Select((x, i) => ConvertToOperation(x, i, fs)).ToList();
        return new ModInfoOperations(replaceOperations, editOperations);
    }

    /// <summary>
    /// For every edit of misc.vpp or table.vpp, add similar edits for second file. Only for XTBL edits/replacements!
    /// </summary>
    private IEnumerable<IChange> MirrorMiscTableChanges(IFileSystem fs, IReadOnlyList<IChange> changes)
    {
        foreach (var change in changes)
        {
            var vppPaths = GetPaths(fs, change.File);
            if (!vppPaths.File.EndsWith(".xtbl", StringComparison.InvariantCultureIgnoreCase))
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

            log.LogTrace("Mirroring changes from [{archive}] to [{mirror}]", vppPaths.Archive, mirror);
            var mirrorChange = change.Clone();
            mirrorChange.File = fs.Path.Combine(mirror, vppPaths.File);
            yield return mirrorChange;
        }
    }

    private FileSwapOperation ConvertToOperation(Replace replace, int index, IFileSystem fs, IImmutableDictionary<VppPath, IFileInfo> referencedFiles) => new FileSwapOperation(index, GetPaths(fs, replace.File), referencedFiles[GetPaths(fs, replace.File)]);

    private XmlEditOperation ConvertToOperation(Edit edit, int index, IFileSystem fs) => new XmlEditOperation(index, GetPaths(fs, edit.File), edit.LIST_ACTION, edit.NestedXml.ToList());

    /// <summary>
    /// Check, sanitize and split path from Change to "data/vpp" + "relative/file/path"
    /// </summary>
    public VppPath GetPaths(IFileSystem fs, string rawPath)
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
            parts = new[]
                {
                    "data"
                }.Concat(parts[3..])
                .ToArray();
            log.LogTrace("Patched legacy paths from [{original}] to [{patched}]", path, string.Join("/", parts));
        }

        var data = parts[0];
        if (data != "data")
        {
            throw new ArgumentException($"modinfo.xml references wrong vpp to edit: [{path}]. path should start with 'data'");
        }

        var vpp = parts[1];
        if (vpp.EndsWith(".vpp", StringComparison.InvariantCultureIgnoreCase))
        {
            vpp += "_pc";
            log.LogTrace("Patched .vpp archive extension: [{patched}]", vpp);
        }

        if (!vpp.EndsWith(".vpp_pc", StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ArgumentException($"modinfo.xml references wrong vpp to edit: [{path}]. path should reference vpp_pc archive");
        }

        var dataVpp = fs.Path.Combine(data, vpp);
        var archiveRelativePath = fs.Path.Combine(parts[2..]);

        var result = new VppPath(dataVpp, archiveRelativePath);
        log.LogTrace("Parsed [{rawPath}] to [{vppPath}]", rawPath, result);
        return result;
    }

    private static string SanitizePath(string path) =>
        path.Replace("//", "").Replace("..", "").Replace(":", "").ToLowerInvariant();

    /// <summary>
    /// Append to all nested "user_input" tags corresponding selected values
    /// </summary>
    private void InsertUserEditValuesRecursive(XmlNode node, Dictionary<string, XmlNode> selectedValues)
    {
        if (node is not XmlElement element)
        {
            log.LogTrace("Node [{name}] [{type}] is not XmlElement, nothing to insert", node.Name, node.NodeType);
            return;
        }

        if (element.Name.Equals(UserInputName, StringComparison.OrdinalIgnoreCase))
        {
            var key = element.InnerText.ToLowerInvariant();
            log.LogTrace("Processing user input tag for [{key}]", key);
            var replacementHolder = selectedValues[key].Clone();
            if (replacementHolder.OwnerDocument != element.OwnerDocument)
            {
                // cloning every time because nodes can belong to same document and are moved to different places
                log.LogTrace("Cloned selected input value");
                replacementHolder = element.OwnerDocument.ImportNode(replacementHolder, true).Clone();
            }

            var parent = element.ParentNode;
            XmlNode current = element;
            // collection of child nodes shrinks because we actually move elements to another place
            while (replacementHolder.ChildNodes.Count > 0)
            {
                var replacement = replacementHolder.ChildNodes[0]!;
                parent!.InsertAfter(replacement, current);
                current = replacement;
                log.LogTrace("Inserted replacement node [{name}]", replacement.Name);
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
                log.LogTrace("Descending into [{name}] child [{i}]", element.Name, i);
                var nextNode = element.ChildNodes[i];
                InsertUserEditValuesRecursive(nextNode!, selectedValues);
                i++;
            }
        }
    }

    /// <summary>
    /// Remove "user_input" nodes after all edits are done. Returns true if node was removed (outer loop SHOULD NOT advance)
    /// </summary>
    private bool RemoveUserEditNodesRecursive(XmlNode node)
    {
        if (node is not XmlElement element)
        {
            log.LogTrace("Node [{name}] [{type}] is not XmlElement, nothing to remove", node.Name, node.NodeType);
            return false;
        }

        if (element.Name.Equals(UserInputName, StringComparison.OrdinalIgnoreCase))
        {
            var parent = element.ParentNode;
            parent!.RemoveChild(element);
            log.LogTrace("Removed UserInput node from [{name}]", parent.Name);
            return true;
        }

        if (element.HasChildNodes)
        {
            // descend. can't use for/foreach: we modify collection in-place!
            var i = 0;
            while (i < element.ChildNodes.Count)
            {
                log.LogTrace("Descending into [{name}] child [{i}]", element.Name, i);
                var nextNode = element.ChildNodes[i];
                var removed = RemoveUserEditNodesRecursive(nextNode!);
                if (!removed)
                {
                    i++;
                }
            }
        }

        return false;
    }

    private const string UserInputName = "user_input";
}
