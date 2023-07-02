using System.Diagnostics.CodeAnalysis;
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
        progress = new Progress<OperationInfo>(ProgressHandler);
    }

    public async Task<bool> Execute<T>(IReadOnlyList<T> data, Func<T, CancellationTokenSource, CancellationToken, Task> body, int threadCount, TimeSpan period, string operation, string unit, CancellationToken token)
    {
        var total = data.Count;
        var started = DateTime.UtcNow;
        log.LogInformation(Md.H1.ToEventId(), "{operation}: {total} {unit}", operation, total, unit);
        var info = new OperationInfo(new Count(), total, started, new LastTime { Value = started }, period, operation, unit, new List<double>() { 0 }, new List<double>() { 0 });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var task = Parallel.ForEachAsync(data,
            new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = threadCount
            },
            async (x, t) =>
            {
                await body(x, cts, t);
                progress.Report(info);
            });
        var timerTask = RunTimer(period, info, cts.Token);

        try
        {
            await task;
        }
        catch (OperationCanceledException e) when (cts.IsCancellationRequested)
        {
            log.LogTrace(e, "Canceled Parallel.ForEachAsync");
        }

        var allCompleted = !cts.IsCancellationRequested;
        // NOTE: canceling anyway to stop timer
        cts.Cancel();
        await timerTask;
        var finished = DateTime.UtcNow;
        var elapsedSpan = finished - started;
        var elapsed = FormatTimespan(elapsedSpan);
        if (!allCompleted)
        {
            log.LogInformation("{operation}: {i}/{total} {unit}, canceled after {elapsed}", operation, info.Count.Value, total, unit, elapsed);
            return false;
        }

        log.LogInformation("{operation}: {total} {unit}, completed in {elapsed}", operation, total, unit, elapsed);
        return true;
    }

    private async Task RunTimer(TimeSpan period, OperationInfo info, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(period);
            while (await timer.WaitForNextTickAsync(token))
            {
                Tick(info);
            }
        }
        catch (OperationCanceledException e)
        {
            log.LogTrace(e, "Canceled timer.WaitForNextTickAsync");
        }
    }

    private void ProgressHandler(OperationInfo info)
    {
        info.Count.Increment();
        Report(info);
    }

    private void Tick(OperationInfo info) => Report(info);

    private void Report(OperationInfo info)
    {
        var now = DateTime.UtcNow;
        var delta = now - info.LastTime.Value;
        if (delta < info.Period)
        {
            return;
        }

        lock (LockObject)
        {
            var elapsed = now - info.Started;

            info.LastTime.Value = now;
            info.Times.Add(elapsed.TotalSeconds);
            info.Measures.Add(info.Count.Value);

            if (info.Measures.Count < 2)
            {
                log.LogInformation(Md.Bullet.ToEventId(), "{operation}: {i}/{total}, ??? left", info.Operation, info.Count.Value, info.Total);
            }

            var fit = Fit.LineFunc(info.Measures.ToArray(), info.Times.ToArray());
            var estimateSecondsAll = fit(info.Total);
            var estimateSecondsLeft = Math.Max(estimateSecondsAll - elapsed.TotalSeconds, 0);
            var estimate = FormatTimespan(TimeSpan.FromSeconds(estimateSecondsLeft));

            log.LogInformation(Md.Bullet.ToEventId(), "{operation}: {i}/{total}, {estimate} left", info.Operation, info.Count.Value, info.Total, estimate);
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
