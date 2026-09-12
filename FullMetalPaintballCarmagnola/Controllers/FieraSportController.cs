using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [AllowAnonymous]
    public class FieraSportController : Controller
    {
        private const string EventCode = "FieraSportCarmagnola2026";
        private readonly TesseramentoDbContext _dbContext;

        public FieraSportController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("/FieraSportCarmagnola2026")]
        public IActionResult Index()
        {
            return View(new FieraSportLead());
        }

        [HttpPost("/FieraSportCarmagnola2026")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FieraSportLead model)
        {
            if (!model.PrivacyAccepted)
            {
                ModelState.AddModelError(nameof(model.PrivacyAccepted), "Per procedere devi autorizzare il trattamento dei dati.");
            }

            if (!model.LiabilityAccepted)
            {
                ModelState.AddModelError(nameof(model.LiabilityAccepted), "Per procedere devi accettare lo scarico di responsabilita.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var nomeCognome = model.NomeCognome.Trim();
            var email = model.Email.Trim().ToLowerInvariant();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""FieraSportLeads""
                    (""Email"", ""NomeCognome"", ""PrivacyAccepted"", ""LiabilityAccepted"", ""EventCode"", ""CreatedAtUtc"", ""IpAddress"", ""UserAgent"")
                VALUES
                    ({email}, {nomeCognome}, TRUE, TRUE, {EventCode}, (NOW() AT TIME ZONE 'UTC'), {ipAddress}, {userAgent});");

            TempData["FieraSportSuccess"] = "Registrazione ricevuta correttamente. Grazie!";
            return RedirectToAction(nameof(Index));
        }
    }
}
