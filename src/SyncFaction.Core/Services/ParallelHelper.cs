using System.Text.Json;
using MathNet.Numerics;
using Microsoft.Extensions.Logging;
using SyncFaction.Core.Models;

namespace SyncFaction.Core.Services;

public class ParallelHelper
{
    private readonly ILogger<ParallelHelper> log;
    private readonly IProgress<OperationInfo> progress;
    private static readonly object LockObject = new object();

    public ParallelHelper(ILogger<ParallelHelper> log)
    {
        this.log = log;
        progress = new Progress<OperationInfo>(Handler);
    }

    public async Task Execute<T>(IReadOnlyList<T> data, Func<T, CancellationToken, Task> body, int threadCount, TimeSpan period, string operation, string unit, CancellationToken token)
    {
        var total = data.Count;
        var started = DateTime.UtcNow;
        log.LogInformation(LogFlags.H1.ToEventId(), "{operation}: {total} {unit}", operation, total, unit);

        var info = new OperationInfo(new Count(), total, started, new LastTime { Value = started }, period, operation, unit, new List<double>() { 0 }, new List<double>() { 0 });
        await Parallel.ForEachAsync(data,
            new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = threadCount
            },
            async (x, t) =>
            {
                await body(x, t);
                progress.Report(info);
            });

        var finished = DateTime.UtcNow;
        var elapsed = finished - started;
        log.LogInformation("{operation}: {total} {unit} in {elapsed}", operation, total, unit, elapsed);
    }

    private void Handler(OperationInfo info)
    {
        var now = DateTime.UtcNow;
        info.Count.Increment();

        var delta = now - info.LastTime.Value;
        if (delta < info.Period)
        {
            return;
        }

        lock (LockObject)
        {
            info.LastTime.Value = now;
            var elapsed = now - info.Started;
            info.LastTime.Value = now;
            info.Times.Add(elapsed.TotalSeconds);
            info.Measures.Add(info.Count.Value);

            var fit = Fit.LineFunc(info.Measures.ToArray(), info.Times.ToArray());
            var estimateSecondsAll = fit(info.Total);
            var estimateSecondsLeft = Math.Max(estimateSecondsAll - elapsed.TotalSeconds, 0);
            var estimate = FormatTimespan(TimeSpan.FromSeconds(estimateSecondsLeft));

            log.LogInformation(LogFlags.Bullet.ToEventId(), "{operation}: {i}/{total}, {estimate} left", info.Operation, info.Count.Value, info.Total, estimate);
        }
    }

    private record OperationInfo(Count Count, int Total, DateTime Started, LastTime LastTime, TimeSpan Period, string Operation, string Unit, List<double> Times, List<double> Measures);

    private class Count
    {
        private int value;
        public int Value => value;

        public void Increment()
        {
            Interlocked.Increment(ref value);
        }
    }

    private class LastTime
    {
        public DateTime Value { get; set; }
    }

    private static string FormatTimespan(TimeSpan value)
    {
        var minutes = (int) value.TotalMinutes;
        var seconds = value.Seconds;
        return $"{minutes,3}:{seconds:D2}";
    }
}
