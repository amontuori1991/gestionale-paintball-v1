using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class GiftVoucherInput
{
    [Required, RegularExpression("package|amount")] public string Mode { get; set; } = "package";
    [Required, StringLength(100)] public string Recipient { get; set; } = "";
    [Required, StringLength(100)] public string Buyer { get; set; } = "";
    [RegularExpression(@"\+?[0-9]{7,15}"), StringLength(16)] public string? Phone { get; set; }
    [StringLength(100)] public string? From { get; set; }
    [StringLength(240)] public string? Dedication { get; set; }
    [Required, RegularExpression("Adulti|Kids")] public string Type { get; set; } = "Adulti";
    [Range(1, 100)] public int People { get; set; } = 8;
    [Required, RegularExpression(@"^(1|1\.5|2)$")] public string Duration { get; set; } = "1.5";
    public bool Unlimited { get; set; }
    [Range(0, 2)] public int Rabbit { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public PricingCatalog? PricingSnapshot { get; set; }
    [StringLength(180)] public string? Extras { get; set; }
    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true)] public decimal Amount { get; set; }
    public bool ShowAmount { get; set; }
    [Required, RegularExpression("classico|compleanno|natale|valentino|ricorrenza")] public string Theme { get; set; } = "classico";
    public DateOnly IssuedOn { get; set; } = GiftVoucher.Today;
    [System.Text.Json.Serialization.JsonIgnore] public string Summary => Mode == "amount"
        ? $"Buono a valore da {Amount:0.00} euro"
        : $"{Type} / {People} persone / {(Duration == "1.5" ? "1 ora e 30 minuti" : Duration == "1" ? "1 ora" : "2 ore")}";
    [System.Text.Json.Serialization.JsonIgnore] public string PackageExtras => string.Join(" / ",
        new[] { Rabbit > 0 ? $"Caccia al coniglio: {Rabbit} {(Rabbit == 1 ? "costume" : "costumi")}" : null, Extras }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record VoucherEvent(DateTime AtUtc, string Actor, string Action, string? Reason);

public sealed class GiftVoucher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "FMP-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(10));
    public string Payload { get; set; } = "{}";
    public string CompanyJson { get; set; } = "{}";
    public DateOnly IssuedOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public bool Paid { get; set; }
    public bool Redeemed { get; set; }
    public bool Cancelled { get; set; }
    [ConcurrencyCheck] public int Version { get; set; }
    public string HistoryJson { get; set; } = "[]";
    [NotMapped] public GiftVoucherInput Details => JsonSerializer.Deserialize<GiftVoucherInput>(Payload)!;
    [NotMapped] public List<VoucherEvent> History => JsonSerializer.Deserialize<List<VoucherEvent>>(HistoryJson)!;
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Rome"));
    [NotMapped] public string Status => Cancelled ? "Annullato" : Redeemed ? "Riscattato" : ExpiresOn < Today ? "Scaduto" : !Paid ? "Da pagare" : IssuedOn > Today ? "Non ancora valido" : "Disponibile";

    public void Record(string actor, string action, string? reason = null)
    {
        var history = History;
        history.Add(new(DateTime.UtcNow, actor, action, reason));
        HistoryJson = JsonSerializer.Serialize(history);
        Version++;
    }

    public string? ChangeState(string action, bool admin, string actor, string? reason)
    {
        switch (action)
        {
            case "riscatta":
                if (Status != "Disponibile") return "Il buono non e' disponibile per il riscatto.";
                Redeemed = true;
                break;
            case "ripristina":
                if (!Redeemed) return "Il buono non risulta riscattato.";
                if (string.IsNullOrWhiteSpace(reason)) return "Indica il motivo dell'annullamento del riscatto.";
                Redeemed = false;
                break;
            case "pagato" when admin && !Cancelled && !Redeemed && !Paid:
                Paid = true;
                break;
            case "annulla" when admin && !Cancelled && !Redeemed:
                if (string.IsNullOrWhiteSpace(reason)) return "Indica il motivo dell'annullamento.";
                Cancelled = true;
                break;
            default: return "Operazione non consentita.";
        }
        Record(actor, action, reason?.Trim());
        return null;
    }
}
