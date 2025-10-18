using ApiAggregator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiAggregator.Tests;

public class StatisticsServiceTests
{
    private readonly StatisticsService _service;

    public StatisticsServiceTests()
    {
        _service = new StatisticsService();
    }

    [Fact]
    public void RecordRequest_UpdatesStatisticsCorrectly()
    {
        // Arrange & Act
        _service.RecordRequest("TestAPI", 50);
        _service.RecordRequest("TestAPI", 150);
        _service.RecordRequest("TestAPI", 400);

        // Assert
        var stats = _service.GetStatistics();
        var apiStats = stats.ApiStatistics.First(x => x.ApiName == "TestAPI");

        Assert.Equal(3, apiStats.TotalRequests);
        Assert.Equal(200, apiStats.AverageResponseTime);
        Assert.Equal(1, apiStats.PerformanceBuckets.Fast);
        Assert.Equal(1, apiStats.PerformanceBuckets.Average);
        Assert.Equal(1, apiStats.PerformanceBuckets.Slow);
    }

    [Fact]
    public void RecordRequest_IsThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var requestCount = 1000;

        // Act
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(() => _service.RecordRequest("TestAPI", 100)));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var stats = _service.GetStatistics();
        var apiStats = stats.ApiStatistics.First(x => x.ApiName == "TestAPI");
        Assert.Equal(requestCount, apiStats.TotalRequests);
    }

    [Fact]
    public void GetRecentResponseTimes_ReturnsOnlyRecentData()
    {
        // Arrange
        _service.RecordRequest("TestAPI", 100);
        System.Threading.Thread.Sleep(100);

        // Act
        var recentTimes = _service.GetRecentResponseTimes(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Empty(recentTimes);
    }
}
