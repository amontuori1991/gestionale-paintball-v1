using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class WeatherForecastService
{
    private const double CarmagnolaLatitude = 44.8496;
    private const double CarmagnolaLongitude = 7.7203;
    private const string CacheKey = "weather-forecast-carmagnola-hourly";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherForecastService> _logger;

    public WeatherForecastService(HttpClient httpClient, IMemoryCache cache, ILogger<WeatherForecastService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<int, PartitaWeatherViewModel>> GetWeatherForPartiteAsync(IEnumerable<Partita> partite)
    {
        var partiteDaMappare = partite
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Data + p.OraInizio)
            .ToList();

        if (partiteDaMappare.Count == 0)
        {
            return new Dictionary<int, PartitaWeatherViewModel>();
        }

        var oggi = DateTime.Today;
        var ultimaData = partiteDaMappare.Max(p => p.Data.Date);
        var forecastDays = Math.Clamp((ultimaData - oggi).Days + 1, 1, 16);

        try
        {
            var hourlyForecast = await GetHourlyForecastAsync(forecastDays);
            var result = new Dictionary<int, PartitaWeatherViewModel>();

            foreach (var partita in partiteDaMappare)
            {
                var dataOraPartita = partita.Data.Date.Add(partita.OraInizio);
                if (dataOraPartita.Date < oggi || dataOraPartita.Date > oggi.AddDays(15))
                {
                    continue;
                }

                var meteo = hourlyForecast
                    .OrderBy(x => Math.Abs((x.Time - dataOraPartita).TotalMinutes))
                    .FirstOrDefault(x => Math.Abs((x.Time - dataOraPartita).TotalMinutes) <= 60);

                if (meteo == null)
                {
                    continue;
                }

                result[partita.Id] = ToViewModel(meteo);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossibile recuperare il meteo per le partite.");
            return new Dictionary<int, PartitaWeatherViewModel>();
        }
    }

    private async Task<IReadOnlyList<HourlyWeatherPoint>> GetHourlyForecastAsync(int forecastDays)
    {
        var cacheKey = $"{CacheKey}-{forecastDays}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<HourlyWeatherPoint>? cached) && cached != null)
        {
            return cached;
        }

        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={CarmagnolaLatitude}&longitude={CarmagnolaLongitude}&hourly=weather_code,temperature_2m,precipitation_probability&timezone=Europe%2FRome&forecast_days={forecastDays}");

        var response = await _httpClient.GetFromJsonAsync<OpenMeteoResponse>(url)
            ?? throw new InvalidOperationException("Risposta meteo vuota.");

        var points = new List<HourlyWeatherPoint>();
        var hourly = response.Hourly;
        var count = hourly?.Time?.Count ?? 0;

        for (var i = 0; i < count; i++)
        {
            if (!DateTime.TryParse(hourly!.Time![i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                continue;
            }

            points.Add(new HourlyWeatherPoint
            {
                Time = time,
                WeatherCode = GetValue(hourly.WeatherCode, i),
                TemperatureC = GetValue(hourly.Temperature2m, i),
                PrecipitationProbability = GetValue(hourly.PrecipitationProbability, i)
            });
        }

        _cache.Set(cacheKey, points, TimeSpan.FromHours(2));
        return points;
    }

    private static T? GetValue<T>(IReadOnlyList<T>? values, int index) where T : struct
    {
        return values != null && index >= 0 && index < values.Count ? values[index] : null;
    }

    private static PartitaWeatherViewModel ToViewModel(HourlyWeatherPoint point)
    {
        var (icon, label) = MapWeather(point.WeatherCode);
        return new PartitaWeatherViewModel
        {
            Icon = icon,
            Label = label,
            TemperatureC = point.TemperatureC,
            PrecipitationProbability = point.PrecipitationProbability,
            ForecastTime = point.Time
        };
    }

    private static (string Icon, string Label) MapWeather(int? weatherCode)
    {
        return weatherCode switch
        {
            0 => ("\u2600\uFE0F", "Sereno"),
            1 or 2 => ("\uD83C\uDF24\uFE0F", "Poco nuvoloso"),
            3 => ("\u2601\uFE0F", "Nuvoloso"),
            45 or 48 => ("\uD83C\uDF2B\uFE0F", "Nebbia"),
            51 or 53 or 55 or 56 or 57 => ("\uD83C\uDF26\uFE0F", "Pioviggine"),
            61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => ("\uD83C\uDF27\uFE0F", "Pioggia"),
            71 or 73 or 75 or 77 or 85 or 86 => ("\u2744\uFE0F", "Neve"),
            95 or 96 or 99 => ("\u26C8\uFE0F", "Temporale"),
            _ => ("\uD83C\uDF24\uFE0F", "Meteo")
        };
    }

    private sealed class HourlyWeatherPoint
    {
        public DateTime Time { get; set; }
        public int? WeatherCode { get; set; }
        public double? TemperatureC { get; set; }
        public int? PrecipitationProbability { get; set; }
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }
    }

    private sealed class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public List<string>? Time { get; set; }

        [JsonPropertyName("weather_code")]
        public List<int>? WeatherCode { get; set; }

        [JsonPropertyName("temperature_2m")]
        public List<double>? Temperature2m { get; set; }

        [JsonPropertyName("precipitation_probability")]
        public List<int>? PrecipitationProbability { get; set; }
    }
}
