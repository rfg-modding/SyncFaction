using System;
using AngleSharp.Dom;

namespace SyncFaction.Services;

public record Link(string Name, string Url)
{
    public Link(IElement element, string baseUrl) : this(ValidateText(element), ValidateUrl(element, baseUrl))
    {
    }

    protected static string ValidateText(IElement element)
    {
        var text = element.TextContent;

        if(string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Invalid hyperlink without text", nameof(text));
        }

        return text;
    }

    protected static string ValidateUrl(IElement element, string baseUrl)
    {
        var href = element.Attributes["href"]?.Value;

        if(string.IsNullOrEmpty(href) || href.StartsWith("/"))
        {
            throw new ArgumentException("Invalid hyperlink with absolute href", nameof(href));
        }

        return baseUrl.TrimEnd('/') + '/' + href;
    }
}

/*
TODO

markdown rendering https://github.com/whistyun/MdXaml

*/
