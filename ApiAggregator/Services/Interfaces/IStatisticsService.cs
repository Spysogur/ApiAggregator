namespace ApiAggregator.Services.Interfaces;

public interface IStatisticsService
{
    void RecordRequest(string apiName, long responseTimeMs);
    StatisticsSnapshot GetStatistics();
    Dictionary<string, List<long>> GetRecentResponseTimes(TimeSpan timeWindow);
}
