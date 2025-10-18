
using System;

namespace ApiAggregator.Services;

public class NewsApiService : INewsApiService
{
    public readonly ILogger<NewsApiService> _logger;
    public readonly HttpClient _httpClient;
    public readonly IConfiguration _configuration;
    public NewsApiService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<NewsApiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<List<NewsArticle>> GetNewsAsync(string? query, string? category, CancellationToken ct = default)
    {
        try
        {
            var apiKey = _configuration["ExternalApis:NewsApiKey"];
            var endpoint = $"v2/everything?q={Uri.EscapeDataString(query!)}&apiKey={apiKey}";
            

            _logger.LogInformation("Fetching news from URL: {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint, ct);
            
            //response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("News API error {StatusCode}: {Body}", response.StatusCode, content);
                return new List<NewsArticle>();
            }

            var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            var articles = root.GetProperty("articles");
            var newsArticles = new List<NewsArticle>();
            foreach (var article in articles.EnumerateArray())
            {
                var newsArticle = new NewsArticle
                {
                    Source = article.GetProperty("source").GetProperty("name").GetString() ?? "",
                    Title = article.GetProperty("title").GetString() ?? "",
                    Description = article.GetProperty("description").GetString() ?? "",
                    Url = article.GetProperty("url").GetString() ?? "",
                    PublishedAt = article.GetProperty("publishedAt").GetDateTime(),
                    Category = category ?? "General"
                };
                newsArticles.Add(newsArticle);
            }
            return newsArticles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news for query: {Query}, category: {Category}", query, category);
            return new List<NewsArticle>();
        }
    }
}
