using System.Reflection;
using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
BonusRequest Played(int minutes, params string[] staff) => new()
{
    Staff = BonusAnswer.Yes, Customer = BonusAnswer.Yes, Outcome = BonusGameOutcome.Played,
    Minutes = minutes, Attendees = staff.ToList()
};

var example = new BonusWeek
{
    Requests = new() { Played(60, "Simone", "Davide"), Played(60, "Simone", "Davide"),
        Played(60, "Simone", "Enrico"), Played(60, "Davide", "Enrico"), Played(60, "Simone", "Davide") }
};
var result = BonusPoolRules.Calculate(example);
Check(result.Pool == 100 && result.Payable == 100 && !result.Blocked && result.Pending == 0, "Original example pool");
Check(result.Shares.Single(s => s.Name == "Simone").Amount == 40, "Simone 40 EUR");
Check(result.Shares.Single(s => s.Name == "Davide").Amount == 40, "Davide 40 EUR");
Check(result.Shares.Single(s => s.Name == "Enrico").Amount == 20, "Enrico 20 EUR");

example.Requests.Add(new BonusRequest { Staff = BonusAnswer.Yes, Customer = BonusAnswer.No });
result = BonusPoolRules.Calculate(example);
Check(result.Pool == 100 && !result.Blocked && result.Pending == 0, "Client refusal is neutral");
example.Requests.Add(new BonusRequest { Staff = BonusAnswer.No });
result = BonusPoolRules.Calculate(example);
Check(result.Pool == 100 && result.Blocked && result.Payable == 0 && result.Shares.All(s => s.Amount == 0), "One unavailable staff request blocks all shares");
Check(result.Pending == 0, "Staff refusal needs no client response");

var uneven = new BonusWeek { Requests = new() { Played(60, "Simone", "Davide"), Played(120, "Simone") } };
result = BonusPoolRules.Calculate(uneven);
Check(result.Pool == 50 && result.Shares.Single(s => s.Name == "Simone").Amount == 33.33m
    && result.Shares.Single(s => s.Name == "Davide").Amount == 16.67m, "Equal attendance weighting regardless of duration");
Check(new BonusRates().ForDuration(90) == 25, "Ninety-minute contribution");
uneven.Requests.Add(new BonusRequest { Staff = BonusAnswer.Yes, Customer = BonusAnswer.Yes, Outcome = BonusGameOutcome.Cancelled });
Check(BonusPoolRules.Calculate(uneven).Pool == 50 && BonusPoolRules.Calculate(uneven).Pending == 0, "Cancelled game contributes nothing");
uneven.Requests.Add(new BonusRequest());
Check(BonusPoolRules.Calculate(uneven).Pending == 1, "Pending availability prevents closure");
uneven.Requests[^1].Staff = BonusAnswer.Yes;
Check(BonusPoolRules.Calculate(uneven).Pending == 1, "Pending customer prevents closure");
uneven.Requests[^1].Customer = BonusAnswer.Yes;
Check(BonusPoolRules.Calculate(uneven).Pending == 1, "Unresolved played outcome prevents closure");

var duplicate = new BonusWeek { Requests = new() { Played(60, "Simone", "SIMONE") } };
Check(BonusPoolRules.Calculate(duplicate).Shares.Single().Attendances == 1, "Duplicate staff in one game counts once");
Check(BonusPoolRules.Monday(new DateOnly(2027, 1, 1)) == new DateOnly(2026, 12, 28), "Cross-year week");
Check(BonusPoolRules.Monday(new DateOnly(2026, 9, 27)) == new DateOnly(2026, 9, 21), "Sunday uses preceding Monday");
Check(!BonusPoolRules.IsWeekday(new DateOnly(2026, 9, 26)) && !BonusPoolRules.IsWeekday(new DateOnly(2026, 9, 27)), "No weekends");
Check(BonusPoolRules.Calculate(new BonusWeek()).Payable == 0, "Empty week");
var defaultRates = new BonusRates();
var frozen = new BonusWeek { Rates = defaultRates.Copy(), Requests = new() { Played(60, "Simone") } };
defaultRates.Hour = 70;
Check(BonusPoolRules.Calculate(frozen).Pool == 20, "Previously opened week preserves rates");
frozen.Snapshot = BonusPoolRules.Calculate(frozen);
var reloaded = JsonSerializer.Deserialize<BonusWeek>(JsonSerializer.Serialize(frozen))!;
reloaded.Requests.Clear();
Check(reloaded.Snapshot!.Pool == 20 && reloaded.Snapshot.Shares.Single().Amount == 20, "Saved snapshot is independent of later source edits");

var random = new Random(42);
for (var trial = 0; trial < 300; trial++)
{
    var sample = new BonusWeek { Rates = new BonusRates { Hour = random.Next(1, 10000) / 100m } };
    for (var game = 0; game < random.Next(1, 20); game++)
        sample.Requests.Add(Played(60, Enumerable.Range(0, random.Next(1, 5)).Select(n => "Staff " + random.Next(1, 15)).ToArray()));
    var calc = BonusPoolRules.Calculate(sample);
    Check(calc.Shares.Sum(s => s.Amount) == calc.Pool && calc.Shares.All(s => s.Amount >= 0 && decimal.Round(s.Amount, 2) == s.Amount), "All cents allocated exactly");
}

foreach (var method in typeof(BonusPoolController).GetMethods().Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null))
{
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles == "Admin"), method.Name + " requires Admin");
    Check(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null, method.Name + " validates antiforgery");
}
Check(typeof(BonusPoolController).GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy == "Bonus Pool"), "Feature permission required");
Console.WriteLine($"PASS: {checks} Bonus Pool rules and access checks.");
if (args.Contains("--database")) await DatabaseChecks.Run();
if (args.Contains("--view") || args.Contains("--preview")) await ViewChecks.Run(args.Contains("--preview"));
