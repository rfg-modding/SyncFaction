using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public partial class ListBox : Input
{
    [XmlIgnore]
    public List<IOption> DisplayOptions => displayOptionsOnce.Value;

    [XmlIgnore]
    public override XmlNode? SelectedValue => DisplayOptions[SelectedIndex].ValueHolder;

    /// <summary>
    /// editable option, experimental
    /// </summary>
    [XmlAttribute]
    public bool AllowCustom { get; set; } = true;

    /// <summary>
    /// no-op option, experimental
    /// </summary>
    [XmlAttribute]
    public bool AllowNop { get; set; } = true;

    [XmlAttribute]
    public string SameOptionsAs { get; set; }

    /// <summary>
    /// options from xml
    /// </summary>
    [XmlElement("Option")]
    public List<Option> XmlOptions { get; set; }

    [XmlIgnore]
    public int SelectedIndex { get; set; }

    private readonly Lazy<List<IOption>> displayOptionsOnce;

    public ListBox() => displayOptionsOnce = new Lazy<List<IOption>>(() => InitDisplayOptions().ToList());

    /// <summary>
    /// For debug printing as json
    /// </summary>
    public bool ShouldSerializeSelectedValue() => false;

    private IEnumerable<IOption> InitDisplayOptions()
    {
        foreach (var xmlOption in XmlOptions)
        {
            yield return xmlOption;
        }

        if (AllowNop)
        {
            yield return new NopOption();
        }

        if (AllowCustom)
        {
            yield return new CustomOption();
        }
    }
}
