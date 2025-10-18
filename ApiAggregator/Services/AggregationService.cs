
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace ApiAggregator.Services;

public class AggregationService : IAggregationService
{ 
    private readonly IWeatherApiService _weatherApiService;
    private readonly IStatisticsService _statisticsService;
    private readonly IGitHubApiService _gitHubApiService;
    private readonly INewsApiService _newsApiService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AggregationService> _logger;
    public AggregationService(
        IWeatherApiService weatherApiService,
        IStatisticsService statisticsService,
        IGitHubApiService gitHubApiService,
        INewsApiService newsApiService,
        IMemoryCache cache,
        ILogger<AggregationService> logger)
    {
        _weatherApiService = weatherApiService;
        _statisticsService = statisticsService;
        _gitHubApiService = gitHubApiService;
        _newsApiService = newsApiService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AggregatedResponse> AggregateDataAsync(AggregationRequest request, CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(request);
        //check cache first if data exists returns data from cache else fetch from APIs
        if (_cache.TryGetValue(cacheKey, out AggregatedResponse? cachedResponse))
        {
            _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            return cachedResponse!;
        }

        var response = new AggregatedResponse();
        var errors = new List<string>();
        // Use concurrent bags for thread-safe collections
        var weatherItems = new ConcurrentBag<WeatherData>();
        var newsItems = new ConcurrentBag<NewsArticle>();
        var githubItems = new ConcurrentBag<GitHubRepository>();

        var tasks = new List<Task> {
        FetchWeatherDataAsync(request, weatherItems, errors, ct),
        FetchNewsDataAsync(request, newsItems, errors, ct),
        FetchGitHubDataAsync(request, githubItems, errors, ct)
    };

        await Task.WhenAll(tasks);

        // Convert to sections
        response.Weather.Items = weatherItems.ToList();
        response.Weather.Count = weatherItems.Count;

        var newsList = newsItems.ToList();
        response.News.Items = FilterAndSortNews(newsList, request);
        response.News.Count = response.News.Items.Count;

        var githubList = githubItems.ToList();
        response.GitHub.Items = FilterAndSortGitHub(githubList, request);
        response.GitHub.Count = response.GitHub.Items.Count;

        response.TotalCount = response.Weather.Count + response.News.Count + response.GitHub.Count;
        response.Metadata.WeatherItemsCount = response.Weather.Count;
        response.Metadata.NewsItemsCount = response.News.Count;
        response.Metadata.GitHubItemsCount = response.GitHub.Count;
        response.Metadata.Errors = errors;

        // Store the response in cache
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) // Cache duration
        };
        _cache.Set(cacheKey, response, cacheOptions);
        return response;
    }

    private async Task FetchWeatherDataAsync(
        AggregationRequest request,
        ConcurrentBag<WeatherData> weatherItems,
        List<string> errors,  
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var weatherData = await _weatherApiService.GetWeatherAsync(request.SearchTerm ?? "London", ct);

            foreach (var weather in weatherData)
            {
                weatherItems.Add(new WeatherData
                {
                    City = weather.City,
                    Temperature = weather.Temperature,
                    Description = weather.Description,
                    Humidity = weather.Humidity,
                    WindSpeed = weather.WindSpeed,
                    Latitude = weather.Latitude,
                    Longitude = weather.Longitude,
                    Date = weather.Date
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather data");
            errors.Add($"Weather API: {ex.Message}");
        }
        finally
        {
            sw.Stop();
            _statisticsService.RecordRequest("WeatherAPI", sw.ElapsedMilliseconds);
        }
    }
  

    private async Task FetchNewsDataAsync(
        AggregationRequest request,
        ConcurrentBag<NewsArticle> newsItems,
        List<string> errors, 
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var newsArticles = await _newsApiService.GetNewsAsync(request.SearchTerm, request.Category, ct);

            foreach (var article in newsArticles)
            {
                newsItems.Add(new NewsArticle
                {
                    Source = article.Source,
                    Title = article.Title,
                    Description = article.Description,
                    Url = article.Url,
                    PublishedAt = article.PublishedAt,
                    Category = article.Category ?? "General"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch news data");
            errors.Add($"News API: {ex.Message}");
        }
        finally
        {
            sw.Stop();
            _statisticsService.RecordRequest("NewsAPI", sw.ElapsedMilliseconds);
        }
    }

    private async Task FetchGitHubDataAsync(
        AggregationRequest request,
        ConcurrentBag<GitHubRepository> githubItems,
        List<string> errors, 
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var gitHubRepos = await _gitHubApiService.SearchRepositoriesAsync(request.SearchTerm ?? "dotnet", ct);

            foreach (var repo in gitHubRepos)
            {
                githubItems.Add(new GitHubRepository
                {
                    Name = repo.Name,
                    FullName = repo.FullName,
                    Description = repo.Description,
                    HtmlUrl = repo.HtmlUrl,
                    StargazersCount = repo.StargazersCount,
                    Language = repo.Language ?? "N/A",
                    CreatedAt = repo.CreatedAt,
                    UpdatedAt = repo.UpdatedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub data");
            errors.Add($"GitHub API: {ex.Message}");
        }
        finally
        {
            sw.Stop();
            _statisticsService.RecordRequest("GitHubAPI", sw.ElapsedMilliseconds);
        }
    }

    private List<NewsArticle> FilterAndSortNews(List<NewsArticle> items, AggregationRequest request)
    {
        var filtered = items.AsEnumerable();

        if (request.FromDate.HasValue)
            filtered = filtered.Where(x => x.PublishedAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            filtered = filtered.Where(x => x.PublishedAt <= request.ToDate.Value);

        if (!string.IsNullOrEmpty(request.Category))
            filtered = filtered.Where(x => x.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase));

        filtered = request.SortBy?.ToLower() switch
        {
            "date" => request.Ascending ? filtered.OrderBy(x => x.PublishedAt) : filtered.OrderByDescending(x => x.PublishedAt),
            "title" => request.Ascending ? filtered.OrderBy(x => x.Title) : filtered.OrderByDescending(x => x.Title),
            "source" => request.Ascending ? filtered.OrderBy(x => x.Source) : filtered.OrderByDescending(x => x.Source),
            _ => filtered.OrderByDescending(x => x.PublishedAt)
        };

        if (request.MaxResults.HasValue && request.MaxResults > 0)
            filtered = filtered.Take(request.MaxResults.Value);

        return filtered.ToList();
    }

    private List<GitHubRepository> FilterAndSortGitHub(List<GitHubRepository> items, AggregationRequest request)
    {
        var filtered = items.AsEnumerable();

        if (request.FromDate.HasValue)
            filtered = filtered.Where(x => x.CreatedAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            filtered = filtered.Where(x => x.CreatedAt <= request.ToDate.Value);

        filtered = request.SortBy?.ToLower() switch
        {
            "date" => request.Ascending ? filtered.OrderBy(x => x.UpdatedAt) : filtered.OrderByDescending(x => x.UpdatedAt),
            "title" => request.Ascending ? filtered.OrderBy(x => x.FullName) : filtered.OrderByDescending(x => x.FullName),
            "stars" => request.Ascending ? filtered.OrderBy(x => x.StargazersCount) : filtered.OrderByDescending(x => x.StargazersCount),
            _ => filtered.OrderByDescending(x => x.StargazersCount )
        };

        if (request.MaxResults.HasValue && request.MaxResults > 0)
            filtered = filtered.Take(request.MaxResults.Value);

        return filtered.ToList();
    }
    private string GenerateCacheKey(AggregationRequest request)
    {
        return $"agg_{request.SearchTerm}_{request.Category}_{request.FromDate}_{request.ToDate}_{request.SortBy}_{request.Ascending}_{request.MaxResults}";
    }
}
