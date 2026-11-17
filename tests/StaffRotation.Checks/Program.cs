using Full_Metal_Paintball_Carmagnola.Services;

var sat = new DateTime(2026, 11, 21);
var sun = sat.AddDays(1);
RotationGame Game(int id, DateTime d, string? name = null, bool paid = true) =>
    new(id, d, TimeSpan.FromHours(10), 1, paid, null, name, null, null, null, "Kids", 10);
var roster = new List<string> { "Simone", "Davide", "Alberto" };
var presence = from date in new[] { sat, sun } from n in roster select (date, n);
StaffRotation Run(params RotationGame[] games) => StaffRotation.Build(sat.AddDays(-1), roster, games.ToList(), presence, new[] { (sun, (string?)"Alberto") });
void Check(bool ok) { if (!ok) throw new Exception("Rotation check failed"); }
var simple = Run(Game(1, sat), Game(2, sun));
Check(simple.Notices[0].Text.Contains("Simone:"));
Check(simple.Notices[1].Text.Contains("Davide:"));
var conflict = Run(Game(3, sat, "Simone", false), Game(4, sat));
Check(!conflict.Notices.Single().Text.Contains("Simone:"));
Check(conflict.Rows.Single(r => r.Name == "Simone").Next?.Paid == false);
var history = Run(Game(5, sat.AddYears(-1), "Simone"), Game(6, sat.AddDays(-4), "Simone", false));
Check(history.Rows.Single(r => r.Name == "Simone").Last?.Id == 5);
Check(Run(Game(7, sun, "Alberto")).Notices.Count == 0);
Check(Run(Game(8, sat, "Simone"), Game(9, sun, "Simone")).Notices.All(n => n.Game.Id != 8));
var balance = Run(Game(10, sat, "Alberto"), Game(11, sun.AddDays(1), "Alberto"));
Check(balance.Notices.Any(n => n.Game.Id == 10 && n.Text.Contains("Rotazione") && n.Text.Contains("Simone:")));
Console.WriteLine("PASS: weekend preferences, overlap, provisional bookings, history, on-call exclusion and rotation.");
var kids = Game(12, sat, "Simone");
Check(Run(kids).Notices.Count == 0);
Check(Run(kids with { Players = 11 }).Notices.Single().Text.Contains("1/2"));
Check(Run(kids with { Type = "Adulti" }).Notices.Single().Text.Contains("1/2"));
var both = Run(kids with { First = null, Type = "Adulti" }).Notices;
Check(both.Count == 2 && both.Select(n => n.Text.Split(": ").Last()).Distinct().Count() == 2);
Check(Run(kids with { Type = "Adulti", Second = "Esterno", OnCall = "Esterno" }).Notices.Count == 0);
Console.WriteLine("PASS: adult coverage, Kids 10/11 threshold, distinct candidates and assigned on-call coverage.");
