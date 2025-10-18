using ApiAggregator.Models;
using ApiAggregator.Services;
using ApiAggregator.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApiAggregator.Tests;

public class AggregationServiceTests
{
    private readonly Mock<IWeatherApiService> _weatherServiceMock;
    private readonly Mock<INewsApiService> _newsServiceMock;
    private readonly Mock<IGitHubApiService> _githubServiceMock;
    private readonly Mock<IStatisticsService> _statisticsServiceMock;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AggregationService>> _loggerMock;
    private readonly AggregationService _service;

    public AggregationServiceTests()
    {
        _weatherServiceMock = new Mock<IWeatherApiService>();
        _newsServiceMock = new Mock<INewsApiService>();
        _githubServiceMock = new Mock<IGitHubApiService>();
        _statisticsServiceMock = new Mock<IStatisticsService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AggregationService>>();

        _service = new AggregationService(
            _weatherServiceMock.Object,
            _statisticsServiceMock.Object,
            _githubServiceMock.Object,
            _newsServiceMock.Object,
            _cache,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AggregateDataAsync_ReturnsDataFromAllSources()
    {
        var weatherData = new List<WeatherData>
    {
        new()
        {
            City = "London",
            Temperature = 15,
            Description = "Cloudy",
            Date = DateTime.UtcNow,
            Humidity = 70,        // Add these
            WindSpeed = 5,        // Add these
            Latitude = 51.5074,   // Add these
            Longitude = -0.1278   // Add these
        }
    };

        var newsData = new List<NewsArticle>
    {
        new()
        {
            Title = "Test News",
            Source = "BBC",
            PublishedAt = DateTime.UtcNow,
            Category = "tech",
            Description = "Test description",  // Add this
            Url = "https://test.com"           // Add this
        }
    };

        var githubData = new List<GitHubRepository>
    {
        new()
        {
            Name = "TestRepo",
            FullName = "user/TestRepo",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,  // Add this
            Language = "C#",
            HtmlUrl = "https://github.com/test",  // Add this
            Description = "Test repo",             // Add this
            StargazersCount = 100                  // Add this
        }
    };

        _weatherServiceMock.Setup(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(weatherData);

        _newsServiceMock.Setup(x => x.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newsData);

        _githubServiceMock.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(githubData);

        var request = new AggregationRequest { SearchTerm = "test" };


        var result = await _service.AggregateDataAsync(request);
        #pragma warning disable


        Assert.NotNull(result); 
        Assert.Equal(3, result.TotalCount);

        Assert.Single(result.Weather.Items);
        Assert.Equal("London", result.Weather.Items.First().City);

        Assert.Single(result.News.Items);
        Assert.Equal("BBC", result.News.Items.First().Source);

        Assert.Single(result.GitHub.Items);
        Assert.Equal("TestRepo", result.GitHub.Items.First().Name);

#pragma warning enable
    }

    [Fact]
    public async Task AggregateDataAsync_HandlesApiFailuresGracefully()
    {

        _weatherServiceMock.Setup(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        _newsServiceMock.Setup(x => x.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewsArticle>
            {
                new() { Title = "Test", Source = "CNN", PublishedAt = DateTime.UtcNow }
            });

        _githubServiceMock.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubRepository>());

        var request = new AggregationRequest();


        var result = await _service.AggregateDataAsync(request);


        Assert.NotNull(result);
        Assert.Single(result.Metadata.Errors);
        Assert.Contains("Weather API", result.Metadata.Errors[0]);
    }

    [Fact]
    public async Task AggregateDataAsync_AppliesDateFilter()
    {

        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-1);

        _weatherServiceMock.Setup(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeatherData>());

        _newsServiceMock.Setup(x => x.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewsArticle>
            {
                new() { Title = "Old News", PublishedAt = oldDate, Source = "Test" },
                new() { Title = "Recent News", PublishedAt = recentDate, Source = "Test" }
            });

        _githubServiceMock.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubRepository>());

        var request = new AggregationRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-5)
        };


        var result = await _service.AggregateDataAsync(request);


        Assert.NotNull(result);
        Assert.Single(result.News.Items); // only recent news should remain
        Assert.Equal("Recent News", result.News.Items[0].Title);
        Assert.True(result.News.Items[0].PublishedAt >= request.FromDate);
    }

    [Fact]
    public async Task AggregateDataAsync_AppliesCategoryFilter()
    {

        _weatherServiceMock.Setup(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeatherData>());

        _newsServiceMock.Setup(x => x.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewsArticle>
            {
                new() { Title = "Tech News", Category = "technology", PublishedAt = DateTime.UtcNow, Source = "Test" },
                new() { Title = "Sports News", Category = "sports", PublishedAt = DateTime.UtcNow, Source = "Test" }
            });

        _githubServiceMock.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubRepository>());

        var request = new AggregationRequest { Category = "technology" };


        var result = await _service.AggregateDataAsync(request);


        Assert.NotNull(result);
        Assert.Single(result.News.Items);
        Assert.Equal("technology", result.News.Items[0].Category);
        Assert.Equal("Tech News", result.News.Items[0].Title);
    }

    [Fact]
    public async Task AggregateDataAsync_UsesCacheOnSubsequentRequests()
    {

        _weatherServiceMock.Setup(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeatherData>
            {
                new() { City = "London", Date = DateTime.UtcNow }
            });

        _newsServiceMock.Setup(x => x.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewsArticle>());

        _githubServiceMock.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitHubRepository>());

        var request = new AggregationRequest { SearchTerm = "test" };


        var result1 = await _service.AggregateDataAsync(request);
        var result2 = await _service.AggregateDataAsync(request);


        _weatherServiceMock.Verify(x => x.GetWeatherAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(result1.TotalCount, result2.TotalCount);
    }
}