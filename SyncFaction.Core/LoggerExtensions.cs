using Microsoft.Extensions.Logging;

namespace SyncFaction.Core;

public static class LoggerExtensions
{
    public static void Clear(this ILogger log)
    {
        log.LogCritical(new EventId(0, "clear"), String.Empty);
    }

    public static void LogInformationXaml(this ILogger log, string xaml, bool scroll)
    {
        log.LogInformation(new EventId(0, $"xaml_{scroll}"), xaml);
    }

}
