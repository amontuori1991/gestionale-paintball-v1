namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class PartitaWeatherViewModel
{
    public string Icon { get; set; } = "🌤️";
    public string Label { get; set; } = "Meteo non disponibile";
    public double? TemperatureC { get; set; }
    public int? PrecipitationProbability { get; set; }
    public DateTime ForecastTime { get; set; }

    public string Summary
    {
        get
        {
            var details = new List<string>();

            if (TemperatureC.HasValue)
            {
                details.Add($"{Math.Round(TemperatureC.Value):0}°C");
            }

            if (PrecipitationProbability.HasValue)
            {
                details.Add($"{PrecipitationProbability.Value}% pioggia");
            }

            return details.Count == 0
                ? Label
                : $"{Label} · {string.Join(" · ", details)}";
        }
    }
}
