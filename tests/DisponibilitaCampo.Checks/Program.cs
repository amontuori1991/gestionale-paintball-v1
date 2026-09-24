using System.Reflection;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;

var date = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
var build = typeof(DisponibilitaCampoController).GetMethod("BuildGiorno", BindingFlags.NonPublic | BindingFlags.Static)!;
CampoDisponibilitaGiornoViewModel Day(List<Partita> games, CampoChiusura? closure = null) =>
    (CampoDisponibilitaGiornoViewModel)build.Invoke(null, new object?[] { date, games, closure })!;
var day = Day(new()
{
    new Partita { Data = date, OraInizio = new TimeSpan(9,30,0), Durata = 2 },
    new Partita { Data = date, OraInizio = new TimeSpan(15,0,0), Durata = 1 }
});
var count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    count++;
}
Check(day.Fasce.Any(f => f.Prenotabile && f.Inizio == new TimeSpan(12,0,0) && f.Fine == new TimeSpan(14,30,0)), "Reproduces reported free interval");
Check(day.IsAvailable(new(12,0,0), new(13,0,0)), "Start exactly at 12:00 is free");
Check(day.IsAvailable(new(13,30,0), new(14,30,0)), "End exactly at 14:30 is free");
Check(day.IsAvailable(new(16,30,0), new(17,30,0)), "Start exactly at 16:30 is free");
Check(!day.IsAvailable(new(11,59,0), new(12,59,0)), "One minute overlap at start rejected");
Check(!day.IsAvailable(new(13,31,0), new(14,31,0)), "One minute overlap at end rejected");
Check(!day.IsAvailable(new(14,0,0), new(17,0,0)), "Cannot cross an occupied block");
Check(!day.IsAvailable(new(8,0,0), new(9,0,0)), "Before opening rejected");
Check(day.IsAvailable(day.UltimaFinePartita - TimeSpan.FromHours(1), day.UltimaFinePartita), "Can end exactly at sunset cutoff");
Check(!day.IsAvailable(day.UltimaFinePartita - TimeSpan.FromMinutes(59), day.UltimaFinePartita + TimeSpan.FromMinutes(1)), "After sunset cutoff rejected");
Check(!day.IsAvailable(new(12,0,0), new(12,30,0)), "Minimum duration preserved");
Check(!Day(new(), new CampoChiusura()).IsAvailable(new(12,0,0), new(13,0,0)), "Closed field rejected");
Check(Day(new()).IsAvailable(new(9,0,0), new(10,0,0)), "Empty day allows opening time");
Console.WriteLine($"PASS: {count} availability regression checks. No database or external services used.");
