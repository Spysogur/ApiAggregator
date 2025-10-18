namespace ApiAggregator.Models;


public class AggregationRequest
{

    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; } = "date";
    public bool Ascending { get; set; } = false;
    public int? MaxResults { get; set; }
}
public class AggregatedResponse
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Organized by source
    public WeatherSection Weather { get; set; } = new();
    public NewsSection News { get; set; } = new();
    public GitHubSection GitHub { get; set; } = new();

    // Legacy flat list (optional - keep for backwards compatibility)
    public List<AggregationItem> Data { get; set; } = new();

    public int TotalCount { get; set; }
    public AggregationMetadata Metadata { get; set; } = new();
}

public class AggregationItem
{
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime UpdateTime { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class AggregationMetadata
{
    public int WeatherItemsCount { get; set; }
    public int NewsItemsCount { get; set; }
    public int GitHubItemsCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
public class WeatherSection
{
    public int Count { get; set; }
    public List<WeatherData> Items { get; set; } = new();
}

public class WeatherData
{
    public string City { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Temperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class NewsSection
{
    public int Count { get; set; }
    public List<NewsArticle> Items { get; set; } = new();
}
public class NewsArticle
{
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string Category { get; set; } = string.Empty;

}
public class GitHubSection
{
    public int Count { get; set; }
    public List<GitHubRepository> Items { get; set; } = new();
}

public class GitHubRepository
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public int StargazersCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Language { get; set; } = string.Empty;

}

public class ApiRequestStatistics
{
    public string ApiName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public PerformanceBuckets PerformanceBuckets { get; set; } = new();
}

public class PerformanceBuckets
{
    public int Fast { get; set; }
    public int Average { get; set; }
    public int Slow { get; set; }
}

public class StatisticsSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<ApiRequestStatistics> ApiStatistics { get; set; } = new();
}

public class TokenValidationResponse
{
    public bool Valid { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime AuthenticatedAt { get; set; }
    public object? Claims { get; set; }
}
public class AuthStatusResponse
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public string Message { get; set; } = string.Empty;
}