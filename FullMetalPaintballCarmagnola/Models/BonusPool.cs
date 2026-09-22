using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public enum BonusAnswer { Pending, Yes, No }
public enum BonusGameOutcome { Pending, Played, Cancelled }

public class BonusRates
{
    public decimal Hour { get; set; } = 20m;
    public decimal NinetyMinutes { get; set; } = 25m;
    public decimal TwoHours { get; set; } = 30m;
    public decimal ForDuration(int minutes) => minutes switch
    {
        60 => Hour, 90 => NinetyMinutes, 120 => TwoHours,
        _ => throw new InvalidOperationException("Durata non prevista dal bonus.")
    };
    public BonusRates Copy() => new() { Hour = Hour, NinetyMinutes = NinetyMinutes, TwoHours = TwoHours };
}

public class BonusRequest
{
    public Guid Id { get; set; }
    [Required, StringLength(120)] public string Contact { get; set; } = "";
    [Required, StringLength(5)] public string Prefix { get; set; } = "+39";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; } = new(10, 0);
    public int Minutes { get; set; } = 60;
    public BonusAnswer Staff { get; set; }
    public BonusAnswer Customer { get; set; }
    public BonusGameOutcome Outcome { get; set; }
    public int? PartitaId { get; set; }
    public List<string> Attendees { get; set; } = new();
    [StringLength(1000)] public string? Notes { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = "";
}

public class BonusShare
{
    public string Name { get; set; } = "";
    public int Attendances { get; set; }
    public decimal Amount { get; set; }
}

public class BonusCalculation
{
    public decimal Pool { get; set; }
    public bool Blocked { get; set; }
    public int Pending { get; set; }
    public List<BonusShare> Shares { get; set; } = new();
    public decimal Payable => Blocked ? 0 : Pool;
}

public class BonusWeek
{
    public DateOnly Monday { get; set; }
    public BonusRates Rates { get; set; } = new();
    public List<BonusRequest> Requests { get; set; } = new();
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
    public BonusCalculation? Snapshot { get; set; }
}

public class BonusPoolState
{
    public long Version { get; set; }
    public BonusRates Rates { get; set; } = new();
    public List<BonusWeek> Weeks { get; set; } = new();
}

public class BonusPoolViewModel
{
    public BonusPoolState State { get; set; } = new();
    public BonusWeek Week { get; set; } = new();
    public BonusCalculation Calculation { get; set; } = new();
    public BonusRequest Form { get; set; } = new();
    public List<Partita> Games { get; set; } = new();
    public List<string> Staff { get; set; } = new();
    public bool CanClose { get; set; }
}
