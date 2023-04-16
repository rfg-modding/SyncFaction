using System.Xml;
using System.Xml.Serialization;

namespace SyncFaction.ModManager.XmlModels;

public class ListBox : Input
{
    public ListBox()
    {
        displayOptionsOnce = new Lazy<List<IOption>>(() => InitDisplayOptions().ToList());
    }

    /// <summary>
    /// "default" option which does nothing. TODO experimental. maybe not a good idea because "input value" is separated from a "change operation"
    /// </summary>
    //[XmlAttribute]
    //public bool HasDefault { get; set; } = false;

    /// <summary>
    /// editable option. TODO experimental
    /// </summary>
    [XmlAttribute]
    public bool AllowCustom { get; set; } = true;

    [XmlAttribute] public string SameOptionsAs { get; set; }

    /// <summary>
    /// options from xml
    /// </summary>
    [XmlElement("Option")]
    public List<Option> XmlOptions { get; set; }

    [XmlIgnore]
    public List<IOption> DisplayOptions => displayOptionsOnce.Value;

    [XmlIgnore] public int SelectedIndex { get; set; }

    [XmlIgnore]
    public override XmlNode? SelectedValue => DisplayOptions[SelectedIndex].ValueHolder;

    /// <summary>
    /// For debug printing with Newtonsoft serializer
    /// </summary>
    public bool ShouldSerializeSelectedValue() => false;

    private readonly Lazy<List<IOption>> displayOptionsOnce;

    private IEnumerable<IOption> InitDisplayOptions()
    {
        // if (HasDefault)
        // {
        //     yield return new DefaultOption();
        // }

        foreach (var xmlOption in XmlOptions)
        {
            yield return xmlOption;
        }

        if (AllowCustom)
        {
            yield return new CustomOption();
        }
}
}
