namespace ApiAggregator.Services.Interfaces;

public interface IGitHubApiService
{
    Task<List<GitHubRepository>> SearchRepositoriesAsync(string query, CancellationToken ct = default);
}
