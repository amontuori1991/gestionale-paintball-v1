using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

            var lead = new FieraSportLead
            {
                NomeCognome = model.NomeCognome.Trim(),
                Email = model.Email.Trim().ToLowerInvariant(),
                PrivacyAccepted = true,
                LiabilityAccepted = true,
                EventCode = EventCode,
                CreatedAtUtc = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };

            _dbContext.FieraSportLeads.Add(lead);
            await _dbContext.SaveChangesAsync();

            TempData["FieraSportSuccess"] = "Registrazione ricevuta correttamente. Grazie!";
            return RedirectToAction(nameof(Index));
        }
    }
}
