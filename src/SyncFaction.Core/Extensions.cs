using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Data;


namespace SyncFaction.Core;

public static class Extensions
{
    public static void Clear(this ILogger log)
    {
        log.LogCritical(new EventId(0, "clear"), String.Empty);
    }

    public static void LogInformationXaml(this ILogger log, string xaml, bool scroll)
    {
        log.LogInformation(new EventId(0, $"xaml_{scroll}"), xaml);
    }
    /// <summary>
    /// Filters out common clutter and mod archives
    /// </summary>
    public static bool IsModContent(this IFileInfo f)
    {
        var name = f.Name.ToLowerInvariant();
        var ext = Path.GetExtension(name);
        return !name.StartsWith(".mod") && !Constants.IgnoredExtensions.Contains(ext);
    }

    /// <summary>
    /// Returns update list to install, excluding installed ones if they are in matching order
    /// </summary>
    public static IEnumerable<T?> FilterUpdateList<T>(this IEnumerable<T?> installedUpdates, IEnumerable<T?> newUpdates) => newUpdates
        .Zip(installedUpdates.Concat(Repeat(default(T))))
        .SkipWhile(x => x.First?.Equals(x.Second) is true)
        .Select(x => x.First);

    [SuppressMessage("ReSharper", "IteratorNeverReturns", Justification = "Infinite collection intended as a padding for Zip()")]
    private static IEnumerable<T?> Repeat<T>(T? item)
    {
        while (true)
        {
            yield return item;
        }
    }
}
