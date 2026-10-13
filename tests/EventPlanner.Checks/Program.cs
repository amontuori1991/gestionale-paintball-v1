using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;

var day = new DateTime(2026, 11, 15);
var today = new DateTime(2026, 10, 13);
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
List<EventProposal> Run(int duration = 240, List<Partita>? bookings = null, List<CampoChiusura>? closures = null,
    HashSet<DateTime>? unknown = null, string days = "entrambi") =>
    EventPlanner.Calculate(day, day, duration, days, today, bookings ?? new(), closures ?? new(), unknown ?? new());
var empty = Run();
Check(empty.Count == 1 && empty[0].Occupazione == null, "No history must not mean zero demand");
Check(empty[0].Chiusura == "16:30", "Winter sunset closing");
Check(Run(600).Count == 0, "Long event exceeds daylight");
Check(Run(days: "sabato").Count == 0, "Weekday filter");
Check(Run(unknown: new() { day }).Count == 0, "Unknown event duration blocks day");
Check(Run(closures: new() { new() { DataInizio = day, DataFine = day } }).Count == 0, "Full closure");
var pending = new Partita { Data = day, OraInizio = TimeSpan.FromHours(9), Durata = 2, CaparraConfermata = false };
var blocked = Run(bookings: new() { pending });
Check(blocked.Single().Inizio == "11:30", "Pending booking and buffer respected");
pending.IsDeleted = true;
Check(Run(bookings: new() { pending }).Single().Inizio == "09:00", "Deleted booking does not block");
var history = new List<Partita>();
for (var d = new DateTime(2025, 10, 26); d <= new DateTime(2025, 12, 7); d = d.AddDays(7))
    history.Add(new() { Data = d, OraInizio = TimeSpan.FromHours(9), Durata = 2, CaparraConfermata = true });
var ranked = Run(180, history).Single();
Check(ranked.Campioni >= 3 && ranked.Occupazione == 0 && ranked.Inizio == "11:00", "Prefer historically quieter interval");
var closure = new CampoChiusura { DataInizio = day, DataFine = day, OraInizio = TimeSpan.FromHours(9), OraFine = TimeSpan.FromHours(12) };
Check(Run(240, closures: new() { closure }).Single().Inizio == "12:00", "Partial closure");
Check(Run(300, closures: new() { closure }).Count == 0, "No contiguous interval");
var allDays = EventPlanner.Calculate(day, day.AddDays(7), 180, "entrambi", today, history, new(), new());
Check(allDays.Select(p => p.Data).Distinct().Count() == allDays.Count, "One suggestion per date");
foreach (var p in allDays) Check(TimeSpan.Parse(p.Fine) <= TimeSpan.Parse(p.Chiusura), "Always finish before closing");
Check(EventPlanner.Calculate(day, day, 60, "entrambi", day.AddHours(15).AddMinutes(45), new(), new(), new()).Count == 0,
    "Do not suggest elapsed starts today");
Console.WriteLine($"PASS: {checks} event planner checks");
