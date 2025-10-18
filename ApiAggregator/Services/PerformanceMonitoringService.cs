namespace ApiAggregator.Services;

public class PerformanceMonitoringService : BackgroundService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly Dictionary<string,double> _apiAverages = new();

    public PerformanceMonitoringService(
        ILogger<PerformanceMonitoringService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorPerformanceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance monitoring");
            }
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task MonitorPerformanceAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

        // Get recent response times (last 5 minutes)
        var recentTimes = statisticsService.GetRecentResponseTimes(TimeSpan.FromMinutes(5));

        foreach (var apiData in recentTimes)
        {
            var apiName = apiData.Key;
            var responseTimes = apiData.Value;

            if (!responseTimes.Any())
                continue;

            var recentAverage = responseTimes.Average();

            // Check if we have historical data
            if (_apiAverages.TryGetValue(apiName, out var historicalAverage))
            {
                // Check for 50% performance degradation
                var degradationThreshold = historicalAverage * 1.5;

                if (recentAverage > degradationThreshold)
                {
                    _logger.LogWarning(
                        "PERFORMANCE ANOMALY DETECTED: {ApiName} - Recent avg: {RecentAvg}ms, Historical avg: {HistoricalAvg}ms, Degradation: {Degradation:P0}",
                        apiName,
                        Math.Round(recentAverage, 2),
                        Math.Round(historicalAverage, 2),
                        (recentAverage - historicalAverage) / historicalAverage);
                }
            }

            // Update rolling average (weighted: 70% old, 30% new)
            if (_apiAverages.ContainsKey(apiName))
            {
                _apiAverages[apiName] = (_apiAverages[apiName] * 0.7) + (recentAverage * 0.3);
            }
            else
            {
                _apiAverages[apiName] = recentAverage;
            }

            _logger.LogInformation(
                "Performance check: {ApiName} - Recent 5min avg: {RecentAvg}ms, Rolling avg: {RollingAvg}ms",
                apiName,
                Math.Round(recentAverage, 2),
                Math.Round(_apiAverages[apiName], 2));
        }

        await Task.CompletedTask;
    }
}
