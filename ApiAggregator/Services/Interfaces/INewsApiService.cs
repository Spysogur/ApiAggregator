namespace ApiAggregator.Services.Interfaces;

public interface INewsApiService
{
    Task<List<NewsArticle>> GetNewsAsync(string? query, string? category, CancellationToken ct = default);
}
