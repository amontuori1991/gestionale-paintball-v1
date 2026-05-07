using System.Globalization;

namespace Full_Metal_Paintball_Carmagnola.Models;

public static class PricingSections
{
    public const string AdultsStandard = "AdultsStandard";
    public const string AdultsTournament = "AdultsTournament";
    public const string Kids = "Kids";
    public const string KidsTournament = "KidsTournament";
    public const string ExtraReloads = "ExtraReloads";
    public const string RabbitHunt = "RabbitHunt";
}

public static class PricingEntryCodes
{
    public const string AdultStandard1Hour = "adult-standard-1h";
    public const string AdultStandard90Minutes = "adult-standard-1h30";
    public const string AdultStandard2Hours = "adult-standard-2h";
    public const string AdultUnlimited1Hour = "adult-unlimited-1h";
    public const string AdultUnlimited90Minutes = "adult-unlimited-1h30";
    public const string AdultTournamentStandard = "adult-tournament-standard";
    public const string AdultTournamentUnlimited = "adult-tournament-unlimited";
    public const string Kids1Hour = "kids-1h";
    public const string Kids90Minutes = "kids-1h30";
    public const string Kids2Hours = "kids-2h";
    public const string KidsTournament = "kids-tournament";
    public const string RabbitSingle = "rabbit-single";
    public const string RabbitDouble = "rabbit-double";
    public const string Extra50 = "extra-50";
    public const string Extra100 = "extra-100";
    public const string Extra250 = "extra-250";
    public const string Extra500 = "extra-500";
    public const string Extra1000 = "extra-1000";
    public const string Extra2000 = "extra-2000";
}

public class PricingCatalog
{
    public short CurrentListinoId { get; set; } = 2;

    public string KidsPricingNote { get; set; } = "I prezzi includono colpi illimitati e tesseramento fino al 31/12.";

    public List<PricingListinoDefinition> Listini { get; set; } = new();

    public List<PricingEntry> Entries { get; set; } = new();

    public PricingListinoDefinition GetLegacyListino() =>
        Listini.OrderBy(l => l.Id).FirstOrDefault() ?? new PricingListinoDefinition { Id = 1, Name = "Listino precedente" };

    public PricingListinoDefinition GetCurrentListino() =>
        Listini.FirstOrDefault(l => l.Id == CurrentListinoId)
        ?? Listini.OrderByDescending(l => l.Id).FirstOrDefault()
        ?? new PricingListinoDefinition { Id = 2, Name = "Listino attuale" };

    public string GetListinoName(short listinoId) =>
        Listini.FirstOrDefault(l => l.Id == listinoId)?.Name ?? $"Listino {listinoId}";

    public PricingEntry? GetEntry(string code) =>
        Entries.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));

    public List<PricingEntry> GetSectionEntries(string section) =>
        Entries
            .Where(e => string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Label)
            .ToList();

    public string BuildStandardSummary(short listinoId)
    {
        var targets = new[]
        {
            PricingEntryCodes.AdultStandard1Hour,
            PricingEntryCodes.AdultStandard90Minutes,
            PricingEntryCodes.AdultStandard2Hours
        };

        var prices = targets
            .Select(code => GetEntry(code))
            .Where(entry => entry != null)
            .Select(entry => entry!.GetPrice(listinoId).ToString("0", CultureInfo.InvariantCulture))
            .ToList();

        return prices.Count == 0 ? GetListinoName(listinoId) : $"{GetListinoName(listinoId)} ({string.Join(" / ", prices)})";
    }

    public string BuildKidsSummary(short listinoId)
    {
        var targets = new[]
        {
            PricingEntryCodes.Kids1Hour,
            PricingEntryCodes.Kids90Minutes,
            PricingEntryCodes.Kids2Hours
        };

        var prices = targets
            .Select(code => GetEntry(code))
            .Where(entry => entry != null)
            .Select(entry => entry!.GetPrice(listinoId).ToString("0", CultureInfo.InvariantCulture))
            .ToList();

        return prices.Count == 0 ? GetListinoName(listinoId) : $"{GetListinoName(listinoId)} kids ({string.Join(" / ", prices)})";
    }
}

public class PricingListinoDefinition
{
    public short Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class PricingEntry
{
    public string Code { get; set; } = string.Empty;

    public string Section { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string IncludedShots { get; set; } = string.Empty;

    public decimal? DurationHours { get; set; }

    public decimal Listino1Price { get; set; }

    public decimal Listino2Price { get; set; }

    public int SortOrder { get; set; }

    public string? Notes { get; set; }

    public decimal GetPrice(short listinoId) => listinoId == 1 ? Listino1Price : Listino2Price;
}
