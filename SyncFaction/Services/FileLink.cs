using System.IO;
using AngleSharp.Dom;

namespace SyncFaction.Services;

public record FileLink(string FileName, string Name, string Url) : Link(Name, Url)
{
    public FileLink(IElement element, string baseUrl) : this(GetVppFileName(element), ValidateText(element), ValidateUrl(element, baseUrl))
    {
    }

    protected static string GetVppFileName(IElement element) => $"{Path.GetFileNameWithoutExtension(element.TextContent)}.vpp_pc";
}
