using System;
using System.Linq;
using System.Windows.Controls;
using MdXaml;

namespace SyncFaction;

public class MarkdownRender
{
    private MarkdownScrollViewer view;
    private readonly IProgress<Update> progress;
    private readonly Markdown markdown;
    private ScrollViewer? Scroll => view.Template.FindName("PART_ContentHost", view) as ScrollViewer;


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

    private void Handler(Update update)
    {
        if (string.IsNullOrEmpty(update.Value))
        {
            view.Document.Blocks.Clear();
            return;
        }

        // TODO how to append?!
        var newDoc = markdown.Transform(update.Value);
        view.Document.Blocks.AddRange(newDoc.Blocks.ToList());
        if (update.Scroll)
        {
            Scroll?.ScrollToBottom();
        }
    }

    public void Append(string value, bool autoScroll=true)
    {
        progress.Report(new Update(value, autoScroll));
    }

    public void Clear()
    {
        progress.Report(new Update("", false));
    }

    private record Update(string Value, bool Scroll);

}
