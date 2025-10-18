namespace ApiAggregator.Services;

public class WeatherApiService : IWeatherApiService
{
    private readonly ILogger<WeatherApiService> _logger;
    private readonly HttpClient _httpClient; 
    private readonly IConfiguration _configuration;

    public WeatherApiService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<WeatherApiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient; 
    }

    public async Task<List<WeatherData>> GetWeatherAsync(string city, CancellationToken ct = default)
    {
        var weatherDataList = new List<WeatherData>();
        try
        {
            var apiKey = _configuration["ExternalApis:WeatherApiKey"];
            // First, get the geocoding data to convert location to lat/lon
            var geoUrl = $"geo/1.0/direct?q={Uri.EscapeDataString(city)}&limit=1&appid={apiKey}";
            var geoResponse = await _httpClient.GetAsync(geoUrl, ct);

            geoResponse.EnsureSuccessStatusCode();

            var geoContent = await geoResponse.Content.ReadAsStringAsync(ct);
            var geoResultJson = JsonDocument.Parse(geoContent);
            var geoRoot = geoResultJson.RootElement;
            if (geoRoot.GetArrayLength() == 0)
            {
                _logger.LogWarning("No geocoding results found for location: {Location}", city);
                return weatherDataList;
            }
            var lat = geoRoot[0].GetProperty("lat").GetDouble();
            var lon = geoRoot[0].GetProperty("lon").GetDouble();

            var weatherUrl = $"data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric";
            var weatherResponse = await _httpClient.GetAsync(weatherUrl, ct);
            weatherResponse.EnsureSuccessStatusCode();

            var weathercontent = await weatherResponse.Content.ReadAsStringAsync(ct);
            var json = JsonDocument.Parse(weathercontent);
            var root = json.RootElement;

            var weatherData = new WeatherData
            {
                City = root.GetProperty("name").GetString() ?? city,
                Temperature = root.GetProperty("main").GetProperty("temp").GetDouble(),
                Description = root.GetProperty("weather")[0].GetProperty("description").GetString() ?? "",
                Humidity = root.GetProperty("main").GetProperty("humidity").GetDouble(),
                WindSpeed = root.GetProperty("wind").GetProperty("speed").GetDouble(),
                Date = DateTime.UtcNow
            };
            return new List<WeatherData> { weatherData };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data for location: {Location}", city);
        }
        return weatherDataList;
    }

}
