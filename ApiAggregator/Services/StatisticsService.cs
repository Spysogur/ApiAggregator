using System.Collections.Concurrent;

namespace ApiAggregator.Services;

public class StatisticsService : IStatisticsService
{
    private readonly ConcurrentDictionary<string, ApiStats> _statistics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<TimedRequest>> _recentRequests = new();
    private readonly object _lock = new();

    private class ApiStats
    {
        public long TotalRequests;
        public long TotalResponseTime;
        public long FastCount;
        public long AverageCount;
        public long SlowCount;
    }
    private class TimedRequest
    {
        public long ResponseTimeMs;
        public DateTime Timestamp;
    }
    public void RecordRequest(string apiName, long responseTimeMs)
    {
        var stats = _statistics.GetOrAdd(apiName, _ => new ApiStats());
        Interlocked.Increment(ref stats.TotalRequests);
        Interlocked.Add(ref stats.TotalResponseTime, responseTimeMs);
        if (responseTimeMs < 100)
            Interlocked.Increment(ref stats.FastCount);
        else if (responseTimeMs <= 300)
            Interlocked.Increment(ref stats.AverageCount);
        else
            Interlocked.Increment(ref stats.SlowCount);
        var queue = _recentRequests.GetOrAdd(apiName, _ => new ConcurrentQueue<TimedRequest>());
        queue.Enqueue(new TimedRequest
        {
            ResponseTimeMs = responseTimeMs,
            Timestamp = DateTime.UtcNow
        });

        // Clean up old requests (keep last 10 minutes)
        CleanupOldRequests(apiName, TimeSpan.FromMinutes(10));
    }
    public StatisticsSnapshot GetStatistics()
    {
        var snapshot = new StatisticsSnapshot
        {
            Timestamp = DateTime.UtcNow
        };
        foreach (var kvp in _statistics)
        {
            var stats = kvp.Value;
            var totalRequests = Interlocked.Read(ref stats.TotalRequests);

            if (totalRequests == 0) continue;

            var totalTime = Interlocked.Read(ref stats.TotalResponseTime);
            var avgResponseTime = (double)totalTime / totalRequests;

            snapshot.ApiStatistics.Add(new ApiRequestStatistics
            {
                ApiName = kvp.Key,
                TotalRequests = (int)totalRequests,
                AverageResponseTime = Math.Round(avgResponseTime, 2),
                PerformanceBuckets = new PerformanceBuckets
                {
                    Fast = (int)Interlocked.Read(ref stats.FastCount),
                    Average = (int)Interlocked.Read(ref stats.AverageCount),
                    Slow = (int)Interlocked.Read(ref stats.SlowCount)
                }
            });
        }
        return snapshot;
    }
    public Dictionary<string, List<long>> GetRecentResponseTimes(TimeSpan timeWindow)
    {
        var result = new Dictionary<string, List<long>>();
        var cutoff = DateTime.UtcNow - timeWindow;

        foreach (var kvp in _recentRequests)
        {
            var recentTimes = kvp.Value
                .Where(r => r.Timestamp >= cutoff)
                .Select(r => r.ResponseTimeMs)
                .ToList();

            if (recentTimes.Any())
            {
                result[kvp.Key] = recentTimes;
            }
        }
        return result;
    }

    private void CleanupOldRequests(string apiName, TimeSpan maxAge)
    {
        if (_recentRequests.TryGetValue(apiName, out var queue))
        {
            var cutoff = DateTime.UtcNow - maxAge;
            while (queue.TryPeek(out var request) && request.Timestamp < cutoff)
            {
                queue.TryDequeue(out _);
            }
        }
    }
}