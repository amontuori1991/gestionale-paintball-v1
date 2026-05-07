using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public class PricingCatalogService
{
    private const string PricingCatalogSettingKey = "PricingCatalogV1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TesseramentoDbContext _dbContext;

    public PricingCatalogService(TesseramentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PricingCatalog> GetCatalogAsync()
    {
        var rawValue = await _dbContext.AppSettings
            .Where(s => s.Key == PricingCatalogSettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            var defaultCatalog = BuildDefaultCatalog();
            await SaveCatalogAsync(defaultCatalog);
            return defaultCatalog;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<PricingCatalog>(rawValue, JsonOptions) ?? BuildDefaultCatalog();
            var normalized = NormalizeCatalog(deserialized);

            if (!string.Equals(rawValue, JsonSerializer.Serialize(normalized, JsonOptions), StringComparison.Ordinal))
            {
                await SaveCatalogAsync(normalized);
            }

            return normalized;
        }
        catch
        {
            var fallbackCatalog = BuildDefaultCatalog();
            await SaveCatalogAsync(fallbackCatalog);
            return fallbackCatalog;
        }
    }

    public async Task SaveCatalogAsync(PricingCatalog catalog)
    {
        var normalized = NormalizeCatalog(catalog);
        var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == PricingCatalogSettingKey);

        if (setting == null)
        {
            setting = new AppSetting { Key = PricingCatalogSettingKey };
            _dbContext.AppSettings.Add(setting);
        }

        setting.Value = JsonSerializer.Serialize(normalized, JsonOptions);
        await _dbContext.SaveChangesAsync();
    }

    public static string FormatCurrency(decimal amount) => $"{amount:0.00}€";

    private static PricingCatalog NormalizeCatalog(PricingCatalog catalog)
    {
        var defaults = BuildDefaultCatalog();
        var normalized = new PricingCatalog
        {
            CurrentListinoId = catalog.CurrentListinoId is 1 or 2 ? catalog.CurrentListinoId : (short)2,
            KidsPricingNote = string.IsNullOrWhiteSpace(catalog.KidsPricingNote)
                ? defaults.KidsPricingNote
                : catalog.KidsPricingNote.Trim(),
            Listini = new List<PricingListinoDefinition>(),
            Entries = new List<PricingEntry>()
        };

        foreach (var listinoId in new short[] { 1, 2 })
        {
            var existing = catalog.Listini.FirstOrDefault(l => l.Id == listinoId);
            var fallback = defaults.Listini.First(l => l.Id == listinoId);
            normalized.Listini.Add(new PricingListinoDefinition
            {
                Id = listinoId,
                Name = string.IsNullOrWhiteSpace(existing?.Name) ? fallback.Name : existing!.Name.Trim()
            });
        }

        foreach (var defaultEntry in defaults.Entries.OrderBy(e => e.SortOrder))
        {
            var existing = catalog.Entries.FirstOrDefault(e => string.Equals(e.Code, defaultEntry.Code, StringComparison.OrdinalIgnoreCase));
            normalized.Entries.Add(new PricingEntry
            {
                Code = defaultEntry.Code,
                Section = defaultEntry.Section,
                Label = string.IsNullOrWhiteSpace(existing?.Label) ? defaultEntry.Label : existing!.Label.Trim(),
                IncludedShots = string.IsNullOrWhiteSpace(existing?.IncludedShots) ? defaultEntry.IncludedShots : existing!.IncludedShots.Trim(),
                DurationHours = existing?.DurationHours ?? defaultEntry.DurationHours,
                Listino1Price = Math.Max(0m, existing?.Listino1Price ?? defaultEntry.Listino1Price),
                Listino2Price = Math.Max(0m, existing?.Listino2Price ?? defaultEntry.Listino2Price),
                SortOrder = defaultEntry.SortOrder,
                Notes = string.IsNullOrWhiteSpace(existing?.Notes) ? defaultEntry.Notes : existing!.Notes.Trim()
            });
        }

        return normalized;
    }

    private static PricingCatalog BuildDefaultCatalog()
    {
        return new PricingCatalog
        {
            CurrentListinoId = 2,
            KidsPricingNote = "I prezzi includono colpi illimitati e tesseramento fino al 31/12.",
            Listini = new List<PricingListinoDefinition>
            {
                new() { Id = 1, Name = "Listino precedente" },
                new() { Id = 2, Name = "Listino attuale" }
            },
            Entries = new List<PricingEntry>
            {
                CreateEntry(PricingEntryCodes.AdultStandard1Hour, PricingSections.AdultsStandard, "Partita standard 1 ora", "200", 1.0m, 22m, 25m, 10),
                CreateEntry(PricingEntryCodes.AdultStandard90Minutes, PricingSections.AdultsStandard, "Partita standard 1 ora e 30", "300", 1.5m, 27m, 30m, 20),
                CreateEntry(PricingEntryCodes.AdultStandard2Hours, PricingSections.AdultsStandard, "Partita standard 2 ore", "400", 2.0m, 32m, 35m, 30),
                CreateEntry(PricingEntryCodes.AdultUnlimited1Hour, PricingSections.AdultsStandard, "Partita colpi illimitati 1 ora", "Illimitati", 1.0m, 35m, 38m, 40),
                CreateEntry(PricingEntryCodes.AdultUnlimited90Minutes, PricingSections.AdultsStandard, "Partita colpi illimitati 1 ora e 30", "Illimitati", 1.5m, 42m, 45m, 50),
                CreateEntry(PricingEntryCodes.AdultTournamentStandard, PricingSections.AdultsTournament, "Torneo adulti standard", "200", null, 22m, 25m, 60),
                CreateEntry(PricingEntryCodes.AdultTournamentUnlimited, PricingSections.AdultsTournament, "Torneo adulti colpi illimitati", "Illimitati", null, 35m, 38m, 70),
                CreateEntry(PricingEntryCodes.Kids1Hour, PricingSections.Kids, "Partita kids 1 ora", "Illimitati", 1.0m, 17m, 18m, 80),
                CreateEntry(PricingEntryCodes.Kids90Minutes, PricingSections.Kids, "Partita kids 1 ora e 30", "Illimitati", 1.5m, 22m, 23m, 90),
                CreateEntry(PricingEntryCodes.Kids2Hours, PricingSections.Kids, "Partita kids 2 ore", "Illimitati", 2.0m, 27m, 28m, 100),
                CreateEntry(PricingEntryCodes.KidsTournament, PricingSections.KidsTournament, "Torneo kids", "Illimitati", null, 17m, 18m, 110, "Usato solo se una partita kids e' marcata come torneo."),
                CreateEntry(PricingEntryCodes.Extra50, PricingSections.ExtraReloads, "Ricarica extra 50 colpi", "50", null, 3m, 3m, 120),
                CreateEntry(PricingEntryCodes.Extra100, PricingSections.ExtraReloads, "Ricarica extra 100 colpi", "100", null, 5m, 5m, 130),
                CreateEntry(PricingEntryCodes.Extra250, PricingSections.ExtraReloads, "Ricarica extra 250 colpi", "250", null, 10m, 10m, 140),
                CreateEntry(PricingEntryCodes.Extra500, PricingSections.ExtraReloads, "Ricarica extra 500 colpi", "500", null, 19m, 19m, 150),
                CreateEntry(PricingEntryCodes.Extra1000, PricingSections.ExtraReloads, "Ricarica extra 1000 colpi", "1000", null, 30m, 30m, 160),
                CreateEntry(PricingEntryCodes.Extra2000, PricingSections.ExtraReloads, "Ricarica extra 2000 colpi", "2000", null, 50m, 50m, 170),
                CreateEntry(PricingEntryCodes.RabbitSingle, PricingSections.RabbitHunt, "Caccia al coniglio - 1 costume", "1 costume", null, 60m, 60m, 180),
                CreateEntry(PricingEntryCodes.RabbitDouble, PricingSections.RabbitHunt, "Caccia al coniglio - 2 costumi", "2 costumi", null, 100m, 100m, 190)
            }
        };
    }

    private static PricingEntry CreateEntry(
        string code,
        string section,
        string label,
        string includedShots,
        decimal? durationHours,
        decimal listino1Price,
        decimal listino2Price,
        int sortOrder,
        string? notes = null)
    {
        return new PricingEntry
        {
            Code = code,
            Section = section,
            Label = label,
            IncludedShots = includedShots,
            DurationHours = durationHours,
            Listino1Price = listino1Price,
            Listino2Price = listino2Price,
            SortOrder = sortOrder,
            Notes = notes
        };
    }
}
