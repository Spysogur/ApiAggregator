namespace ApiAggregator.Services;

public class GitHubApiService : IGitHubApiService
{
    public readonly ILogger<GitHubApiService> _logger;
    public readonly HttpClient _httpClient;
    public readonly IConfiguration _configuration;
    public GitHubApiService(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubApiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<List<GitHubRepository>> SearchRepositoriesAsync(string query, CancellationToken ct = default)
    {
        try
        {
 

            // Use relative URL since BaseAddress is set in Program.cs
            var url = $"search/repositories?q={Uri.EscapeDataString(query ?? "dotnet")}&sort=stars&per_page=10";

            _logger.LogInformation("Fetching GitHub repositories for query: {Query}", query);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var json = JsonDocument.Parse(content);
            var items = json.RootElement.GetProperty("items");

            var repositories = new List<GitHubRepository>();
            foreach (var item in items.EnumerateArray())
            {
                repositories.Add(new GitHubRepository
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    FullName = item.GetProperty("full_name").GetString() ?? "",
                    Description = item.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null
                        ? desc.GetString() ?? ""
                        : "",
                    HtmlUrl = item.GetProperty("html_url").GetString() ?? "",
                    StargazersCount = item.GetProperty("stargazers_count").GetInt32(),
                    Language = item.TryGetProperty("language", out var lang) && lang.ValueKind != JsonValueKind.Null
                        ? lang.GetString() ?? ""
                        : "",
                    UpdatedAt = item.GetProperty("updated_at").GetDateTime()
                });
            }

            return repositories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GitHub repositories");
            throw;
        }
    }
}
