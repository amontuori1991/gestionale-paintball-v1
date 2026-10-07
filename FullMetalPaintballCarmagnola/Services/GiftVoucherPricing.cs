using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Services;

public static class GiftVoucherPricing
{
    public static string Key(GiftVoucherInput m) => $"{m.Type}|{(m.Type == "Kids" || m.Unlimited ? "unlimited" : "standard")}|{m.Duration}";

    public static Dictionary<string, decimal> Prices(PricingCatalog catalog)
    {
        var codes = new Dictionary<string, string>
        {
            ["Adulti|standard|1"] = PricingEntryCodes.AdultStandard1Hour,
            ["Adulti|standard|1.5"] = PricingEntryCodes.AdultStandard90Minutes,
            ["Adulti|standard|2"] = PricingEntryCodes.AdultStandard2Hours,
            ["Adulti|unlimited|1"] = PricingEntryCodes.AdultUnlimited1Hour,
            ["Adulti|unlimited|1.5"] = PricingEntryCodes.AdultUnlimited90Minutes,
            ["Kids|unlimited|1"] = PricingEntryCodes.Kids1Hour,
            ["Kids|unlimited|1.5"] = PricingEntryCodes.Kids90Minutes,
            ["Kids|unlimited|2"] = PricingEntryCodes.Kids2Hours,
            ["rabbit1"] = PricingEntryCodes.RabbitSingle,
            ["rabbit2"] = PricingEntryCodes.RabbitDouble
        };
        return codes.Where(pair => catalog.GetEntry(pair.Value) != null)
            .ToDictionary(pair => pair.Key, pair => catalog.GetEntry(pair.Value)!.GetPrice(catalog.CurrentListinoId));
    }

    public static decimal? Calculate(GiftVoucherInput m, PricingCatalog catalog)
    {
        var prices = Prices(catalog);
        if (!prices.TryGetValue(Key(m), out var unit) || unit <= 0 || m.People is < 1 or > 100 || m.Rabbit is < 0 or > 2) return null;
        decimal extra = 0;
        if (m.Rabbit > 0 && !prices.TryGetValue("rabbit" + m.Rabbit, out extra)) return null;
        return decimal.Round(unit * m.People + extra, 2);
    }

    public static bool SamePackage(GiftVoucherInput a, GiftVoucherInput b) =>
        a.Mode == "package" && b.Mode == "package" && Key(a) == Key(b) && a.People == b.People && a.Rabbit == b.Rabbit;
}
