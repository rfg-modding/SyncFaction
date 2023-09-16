using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
        view.MarkdownStyle = MarkdownStyle.SasabuneCompact;
        // init with empty contents
        view.Document.Blocks.Clear();
    }

    public void Append(string value, bool autoScroll) => progress.Report(new Update(value, autoScroll));

    public void AppendXaml(string value, string xaml, bool autoScroll) => progress.Report(new Update(value, autoScroll, xaml));

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

        if (!string.IsNullOrEmpty(update.Xaml))
        {
            var docFromHtml = (FlowDocument) XamlReader.Parse(update.Xaml);
            FixXamlHyperlinks(docFromHtml);

            view.Document.Blocks.AddRange(docFromHtml.Blocks.ToList());
        }

        if (update.Scroll)
        {
            Debug.WriteLine($"bottom before={Scroll.VerticalOffset}");
            Scroll?.ScrollToBottom();
            Debug.WriteLine($"bottom after={Scroll.VerticalOffset}");
        }
        else
        {
            var offset = Scroll.VerticalOffset;
            Debug.WriteLine($"noscroll before={offset}");
            Scroll.ScrollToVerticalOffset(offset);
            Debug.WriteLine($"noscroll before={Scroll.VerticalOffset}");
        }
    }

    private static void FixXamlHyperlinks(FlowDocument document)
    {
        var hyperlinks = GetVisuals(document).OfType<Hyperlink>();
        foreach (var link in hyperlinks)
        {
            link.Command = NavigationCommands.GoToPage;
            link.CommandParameter = link.NavigateUri.ToString();
            link.ToolTip = link.NavigateUri.ToString();
        }
    }

    private static IEnumerable<DependencyObject> GetVisuals(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;

            foreach (var descendants in GetVisuals(child))
            {
                yield return descendants;
            }
        }
    }

    private record Update(string Value, bool Scroll, string? Xaml = null);
}
