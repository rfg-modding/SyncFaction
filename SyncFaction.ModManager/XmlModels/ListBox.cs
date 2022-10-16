using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class ListBox : Input
{
    public ListBox()
    {
        DisplayOptionsOnce = new Lazy<List<IOption>>(() => InitDisplayOptions().ToList());
    }

    /// <summary>
    /// "default" option which does nothing. TODO experimental
    /// </summary>
    [XmlAttribute]
    public bool HasDefault { get; set; } = true;

    /// <summary>
    /// editable option. TODO experimental
    /// </summary>
    [XmlAttribute]
    public bool AllowCustom { get; set; }

    [XmlAttribute] public string SameOptionsAs { get; set; }

    /// <summary>
    /// options from xml
    /// </summary>
    [XmlElement("Option")]
    public List<Option> XmlOptions { get; set; }

    [XmlIgnore]
    public List<IOption> DisplayOptions => DisplayOptionsOnce.Value;

    [XmlIgnore] public int SelectedIndex { get; set; }

    [XmlIgnore] public override XmlNode? SelectedValue => DisplayOptions[SelectedIndex].ValueHolder;

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeValue() => false;

    private Lazy<List<IOption>> DisplayOptionsOnce;

    private IEnumerable<IOption> InitDisplayOptions()
    {
        foreach (var xmlOption in XmlOptions)
        {
            yield return xmlOption;
        }

        if (HasDefault)
        {
            yield return new DefaultOption();
        }

        if (AllowCustom)
        {
            yield return new CustomOption();
        }
}
}
