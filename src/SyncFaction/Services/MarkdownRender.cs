using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using MdXaml;

namespace SyncFaction.Services;

public class MarkdownRender
{
    private ScrollViewer? Scroll => view.Template.FindName("PART_ContentHost", view) as ScrollViewer;
    private readonly IProgress<Update> progress;
    private readonly Markdown markdown;
    private MarkdownScrollViewer view;

    public MarkdownRender(Markdown markdown)
    {
        this.markdown = markdown;
        progress = new Progress<Update>(Handler);
    }

    public void Init(MarkdownScrollViewer markdownScrollViewer)
    {
        view = markdownScrollViewer;
        view.MarkdownStyle = MarkdownStyle.Compact;
        // init with empty contents
        view.Document.Blocks.Clear();
    }

    public void Append(string value, bool autoScroll = true) => progress.Report(new Update(value, autoScroll));

    public void AppendXaml(string value, string xaml, bool autoScroll = true) => progress.Report(new Update(value, autoScroll, xaml));

    public void Clear() => progress.Report(new Update("", false));

    private void Handler(Update update)
    {
        if (string.IsNullOrEmpty(update.Value))
        {
            view.Document.Blocks.Clear();
            return;
        }

        var newDoc = markdown.Transform(update.Value);
        view.Document.Blocks.AddRange(newDoc.Blocks.ToList());

        if (!string.IsNullOrEmpty(update.xaml))
        {
            var docFromHtml = XamlReader.Parse(update.xaml) as FlowDocument;
            view.Document.Blocks.AddRange(docFromHtml.Blocks.ToList());
        }

        if (update.Scroll)
        {
            Scroll?.ScrollToBottom();
        }
    }

    private record Update(string Value, bool Scroll, string? xaml = null);
}
