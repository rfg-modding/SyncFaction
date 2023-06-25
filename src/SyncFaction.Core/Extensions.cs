using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace SyncFaction.Core;

public static class Extensions
{
    public static void Clear(this ILogger log) => log.LogCritical(new EventId(0, "clear"), "");

    public static void LogInformationXaml(this ILogger log, string xaml, bool scroll) => log.LogInformation(new EventId(0, $"xaml_{scroll}"), "{xaml}", xaml);

    /// <summary>
    /// Filters out common clutter and mod archives
    /// </summary>
    public static bool IsModContent(this IFileInfo f)
    {
        var name = f.Name.ToLowerInvariant();
        var ext = Path.GetExtension(name);
        return !name.StartsWith(".mod", StringComparison.OrdinalIgnoreCase) && !Constants.IgnoredExtensions.Contains(ext);
    }

    public static bool IsVppDirectory(this IDirectoryInfo d) => d.FullName.ToLowerInvariant().EndsWith(".vpp_pc", StringComparison.OrdinalIgnoreCase) && !d.Name.Contains(' ');

    /// <summary>
    /// Returns update list to install, excluding installed ones if they are in matching order
    /// </summary>
    public static IEnumerable<T?> FilterUpdateList<T>(this IEnumerable<T?> installedUpdates, IEnumerable<T?> newUpdates) => newUpdates.Zip(installedUpdates.Concat(Repeat(default(T)))).SkipWhile(x => x.First?.Equals(x.Second) is true).Select(x => x.First);

    /// <summary>
    /// Writes xmldoc without declaration to a memory stream. Stream is kept open and rewound to begin
    /// </summary>
    public static void SerializeToMemoryStream(this XmlDocument document, MemoryStream ms)
    {
        using (var tw = XmlWriter.Create(ms,
                   new XmlWriterSettings
                   {
                       CloseOutput = false,
                       //Indent = true, // NOTE: some files cant be reformatted or even minimized, game crashes if you do that
                       Encoding = Utf8NoBom,
                       OmitXmlDeclaration = true
                   }))
        {
            document.WriteTo(tw);
        }
        //document.Save(ms);

        ms.Seek(0, SeekOrigin.Begin);
    }

    [SuppressMessage("ReSharper", "IteratorNeverReturns", Justification = "Infinite collection intended as a padding for Zip()")]
    private static IEnumerable<T?> Repeat<T>(T? item)
    {
        while (true)
        {
            yield return item;
        }
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
}
