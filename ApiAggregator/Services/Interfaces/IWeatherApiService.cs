namespace ApiAggregator.Services.Interfaces;

public interface IWeatherApiService
{
    Task<List<WeatherData>> GetWeatherAsync(string location, CancellationToken ct = default);
}
