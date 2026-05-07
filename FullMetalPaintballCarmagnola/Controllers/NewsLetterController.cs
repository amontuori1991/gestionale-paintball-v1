using System.Net;
using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "NewsLetter")]
    public class NewsLetterController : Controller
    {
        private const string TemplatesSettingKey = "NewsLetterTemplates";
        private const string HistorySettingKey = "NewsLetterHistory";
        private const string ScheduledSettingKey = "NewsLetterScheduled";
        private const string LegacyWebsiteUrl = "https://www.fullmetalpaintballcarmagnola.it/";
        private const string CurrentWebsiteUrl = "https://www.paintballcarmagnola.com/";

        private readonly TesseramentoDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly ILogger<NewsLetterController> _logger;

        public NewsLetterController(
            TesseramentoDbContext dbContext,
            IEmailService emailService,
            ILogger<NewsLetterController> logger)
        {
            _dbContext = dbContext;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? templateId = null)
        {
            await ProcessDueScheduledSendsAsync();

            var templates = await LoadTemplatesAsync();
            var selected = templates.FirstOrDefault(t => t.Id == templateId)
                ?? templates.FirstOrDefault()
                ?? BuildDefaultTemplate();

            var segment = "all";
            return View(new NewsLetterIndexViewModel
            {
                Templates = templates,
                Template = CloneTemplate(selected),
                SegmentoDestinatari = segment,
                DestinatariStimati = await CountNewsletterRecipientsAsync(segment),
                InviiProgrammati = await LoadScheduledAsync(),
                StoricoInvii = await LoadHistoryAsync(),
                EmailAttiveUniche = await CountUniqueNewsletterEmailsAsync(onlyConsenting: true),
                EmailTotaliUniche = await CountUniqueNewsletterEmailsAsync(onlyConsenting: false),
                TesseramentiDisiscritti = await _dbContext.Tesseramenti.CountAsync(t => !t.NewsletterConsent)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTemplate(NewsLetterIndexViewModel model)
        {
            if (!ValidateTemplate(model.Template))
            {
                model.Templates = await LoadTemplatesAsync();
                await PopulateRecipientStatsAsync(model);
                return View("Index", model);
            }

            var templates = await LoadTemplatesAsync();
            var template = NormalizeTemplate(model.Template);
            var existingIndex = templates.FindIndex(t => t.Id == template.Id);

            if (existingIndex >= 0)
            {
                templates[existingIndex] = template;
            }
            else
            {
                templates.Add(template);
            }

            await SaveTemplatesAsync(templates);
            TempData["NewsLetterSuccess"] = "Template salvato correttamente.";
            return RedirectToAction(nameof(Index), new { templateId = template.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTest(NewsLetterIndexViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.EmailTest))
            {
                ModelState.AddModelError(nameof(model.EmailTest), "Inserisci l'indirizzo email per il test.");
            }

            if (!ValidateTemplate(model.Template) || !ModelState.IsValid)
            {
                model.Templates = await LoadTemplatesAsync();
                await PopulateRecipientStatsAsync(model);
                return View("Index", model);
            }

            var emailTest = model.EmailTest!.Trim();

            try
            {
                var template = NormalizeTemplate(model.Template);
                await _emailService.SendEmailAsync(
                    emailTest,
                    template.Oggetto,
                    BuildTemplateHtml(template, null));

                TempData["NewsLetterSuccess"] = $"Email di test inviata a {emailTest}.";
                return RedirectToAction(nameof(Index), new { templateId = template.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'invio newsletter di test a {Destinatario}", emailTest);
                ModelState.AddModelError("", "Errore durante l'invio della newsletter. Verifica SMTP e indirizzo destinatario.");
                model.Templates = await LoadTemplatesAsync();
                await PopulateRecipientStatsAsync(model);
                return View("Index", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBulk(NewsLetterIndexViewModel model)
        {
            if (!ValidateTemplate(model.Template) || !ModelState.IsValid)
            {
                model.Templates = await LoadTemplatesAsync();
                await PopulateRecipientStatsAsync(model);
                return View("Index", model);
            }

            var template = NormalizeTemplate(model.Template);
            var result = await SendNewsletterToSegmentAsync(template, model.SegmentoDestinatari, programmato: false);
            TempData["NewsLetterSuccess"] = $"Invio completato: {result.Inviate} email inviate, {result.Errori} errori.";
            return RedirectToAction(nameof(Index), new { templateId = template.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleBulk(NewsLetterIndexViewModel model)
        {
            if (!model.DataOraProgrammata.HasValue)
            {
                ModelState.AddModelError(nameof(model.DataOraProgrammata), "Inserisci data e ora di invio.");
            }

            if (!ValidateTemplate(model.Template) || !ModelState.IsValid)
            {
                model.Templates = await LoadTemplatesAsync();
                await PopulateRecipientStatsAsync(model);
                return View("Index", model);
            }

            var localDate = DateTime.SpecifyKind(model.DataOraProgrammata!.Value, DateTimeKind.Local);
            var scheduled = await LoadScheduledAsync();
            scheduled.Add(new NewsLetterScheduledSend
            {
                Template = NormalizeTemplate(model.Template),
                SegmentoDestinatari = NormalizeSegment(model.SegmentoDestinatari),
                DataOraProgrammataUtc = localDate.ToUniversalTime()
            });

            await SaveScheduledAsync(scheduled);
            TempData["NewsLetterSuccess"] = "Invio programmato salvato. Verra processato quando la pagina newsletter verra aperta dopo l'orario indicato.";
            return RedirectToAction(nameof(Index), new { templateId = model.Template.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteScheduled(string id)
        {
            var scheduled = await LoadScheduledAsync();
            scheduled.RemoveAll(x => x.Id == id);
            await SaveScheduledAsync(scheduled);

            TempData["NewsLetterSuccess"] = "Invio programmato eliminato.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Disiscriviti(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("DisiscrizioneNewsletter", model: null);
            }

            var tesserato = await _dbContext.Tesseramenti
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.NewsletterUnsubscribeToken == token);

            return View("DisiscrizioneNewsletter", tesserato);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfermaDisiscrizione(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("DisiscrizioneNewsletter", model: null);
            }

            var tesserato = await _dbContext.Tesseramenti
                .FirstOrDefaultAsync(t => t.NewsletterUnsubscribeToken == token);

            if (tesserato == null)
            {
                return View("DisiscrizioneNewsletter", model: null);
            }

            tesserato.NewsletterConsent = false;
            await _dbContext.SaveChangesAsync();

            return View("DisiscrizioneNewsletterConfermata", tesserato);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTemplate(string id)
        {
            var templates = await LoadTemplatesAsync();
            templates.RemoveAll(t => t.Id == id);
            await SaveTemplatesAsync(templates);

            TempData["NewsLetterSuccess"] = "Template eliminato.";
            return RedirectToAction(nameof(Index));
        }

        private bool ValidateTemplate(NewsLetterTemplate template)
        {
            if (template == null)
            {
                ModelState.AddModelError("", "Template non valido.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(template.TestoPulsante) && string.IsNullOrWhiteSpace(template.LinkPulsante))
            {
                ModelState.AddModelError("Template.LinkPulsante", "Inserisci il link del pulsante oppure lascia vuoto anche il testo del pulsante.");
            }

            if (string.IsNullOrWhiteSpace(template.TestoPulsante) && !string.IsNullOrWhiteSpace(template.LinkPulsante))
            {
                ModelState.AddModelError("Template.TestoPulsante", "Inserisci il testo del pulsante oppure lascia vuoto anche il link.");
            }

            return ModelState.IsValid;
        }

        private async Task<List<NewsLetterTemplate>> LoadTemplatesAsync()
        {
            var setting = await _dbContext.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == TemplatesSettingKey);

            if (string.IsNullOrWhiteSpace(setting?.Value))
            {
                return new List<NewsLetterTemplate>();
            }

            try
            {
                var templates = JsonSerializer.Deserialize<List<NewsLetterTemplate>>(setting.Value) ?? new List<NewsLetterTemplate>();
                ReplaceLegacyWebsiteLinks(templates);
                return templates;
            }
            catch
            {
                return new List<NewsLetterTemplate>();
            } 
        }

        private async Task SaveTemplatesAsync(List<NewsLetterTemplate> templates)
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == TemplatesSettingKey);

            if (setting == null)
            {
                setting = new AppSetting { Key = TemplatesSettingKey };
                _dbContext.AppSettings.Add(setting);
            }

            setting.Value = JsonSerializer.Serialize(templates.OrderByDescending(t => t.UpdatedAtUtc).ToList());
            await _dbContext.SaveChangesAsync();
        }

        private async Task PopulateRecipientStatsAsync(NewsLetterIndexViewModel model)
        {
            model.EmailAttiveUniche = await CountUniqueNewsletterEmailsAsync(onlyConsenting: true);
            model.EmailTotaliUniche = await CountUniqueNewsletterEmailsAsync(onlyConsenting: false);
            model.TesseramentiDisiscritti = await _dbContext.Tesseramenti.CountAsync(t => !t.NewsletterConsent);
            model.SegmentoDestinatari = NormalizeSegment(model.SegmentoDestinatari);
            model.DestinatariStimati = await CountNewsletterRecipientsAsync(model.SegmentoDestinatari);
            model.InviiProgrammati = await LoadScheduledAsync();
            model.StoricoInvii = await LoadHistoryAsync();
        }

        private async Task<int> CountUniqueNewsletterEmailsAsync(bool onlyConsenting)
        {
            var query = _dbContext.Tesseramenti
                .AsNoTracking()
                .Where(t => t.Email != null && t.Email != "");

            if (onlyConsenting)
            {
                query = query.Where(t => t.NewsletterConsent);
            }

            var emails = await query
                .Select(t => t.Email)
                .ToListAsync();

            return emails
                .Select(e => e.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .Count();
        }

        private async Task<int> CountNewsletterRecipientsAsync(string segment)
        {
            return (await GetNewsletterRecipientsAsync(segment)).Count;
        }

        private async Task<List<NewsletterRecipient>> GetNewsletterRecipientsAsync(string segment)
        {
            segment = NormalizeSegment(segment);
            var query = _dbContext.Tesseramenti
                .AsNoTracking()
                .Include(t => t.Partita)
                .Where(t => t.NewsletterConsent && t.Email != null && t.Email != "");

            var today = DateTime.UtcNow.Date;
            if (segment == "currentYear")
            {
                var start = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = start.AddYears(1);
                query = query.Where(t => t.Partita != null && t.Partita.Data >= start && t.Partita.Data < end);
            }
            else if (segment == "last12Months")
            {
                var start = today.AddMonths(-12);
                query = query.Where(t =>
                    (t.Partita != null && t.Partita.Data >= start) ||
                    (t.Partita == null && t.DataCreazione >= start));
            }

            var recipients = await query
                .OrderByDescending(t => t.Partita != null ? t.Partita.Data : t.DataCreazione)
                .ThenByDescending(t => t.Id)
                .Select(t => new NewsletterRecipient(t.Email, t.NewsletterUnsubscribeToken))
                .ToListAsync();

            return recipients
                .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .GroupBy(r => r.Email.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();
        }

        private async Task<NewsLetterSendHistoryItem> SendNewsletterToSegmentAsync(NewsLetterTemplate template, string segment, bool programmato)
        {
            segment = NormalizeSegment(segment);
            var recipients = await GetNewsletterRecipientsAsync(segment);
            var sent = 0;
            var errors = 0;

            foreach (var recipient in recipients)
            {
                try
                {
                    var unsubscribeUrl = Url.Action(
                        nameof(Disiscriviti),
                        "NewsLetter",
                        new { token = recipient.UnsubscribeToken },
                        Request.Scheme,
                        Request.Host.ToString());

                    await _emailService.SendEmailAsync(
                        recipient.Email,
                        template.Oggetto,
                        BuildTemplateHtml(template, unsubscribeUrl));
                    sent++;
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex, "Errore durante invio newsletter a {Destinatario}", recipient.Email);
                }
            }

            var historyItem = new NewsLetterSendHistoryItem
            {
                TemplateNome = template.Nome,
                Oggetto = template.Oggetto,
                SegmentoDestinatari = segment,
                Destinatari = recipients.Count,
                Inviate = sent,
                Errori = errors,
                Programmato = programmato,
                SentAtUtc = DateTime.UtcNow
            };

            var history = await LoadHistoryAsync();
            history.Insert(0, historyItem);
            await SaveHistoryAsync(history.Take(40).ToList());

            return historyItem;
        }

        private async Task ProcessDueScheduledSendsAsync()
        {
            var scheduled = await LoadScheduledAsync();
            if (!scheduled.Any())
                return;

            var now = DateTime.UtcNow;
            var due = scheduled.Where(x => x.DataOraProgrammataUtc <= now).ToList();
            if (!due.Any())
                return;

            foreach (var item in due)
            {
                await SendNewsletterToSegmentAsync(NormalizeTemplate(item.Template), item.SegmentoDestinatari, programmato: true);
            }

            scheduled.RemoveAll(x => due.Any(d => d.Id == x.Id));
            await SaveScheduledAsync(scheduled);
            TempData["NewsLetterSuccess"] = $"Processati {due.Count} invii programmati.";
        }

        private async Task<List<NewsLetterSendHistoryItem>> LoadHistoryAsync()
        {
            return await LoadJsonSettingAsync<List<NewsLetterSendHistoryItem>>(HistorySettingKey) ?? new();
        }

        private async Task SaveHistoryAsync(List<NewsLetterSendHistoryItem> history)
        {
            await SaveJsonSettingAsync(HistorySettingKey, history);
        }

        private async Task<List<NewsLetterScheduledSend>> LoadScheduledAsync()
        {
            return await LoadJsonSettingAsync<List<NewsLetterScheduledSend>>(ScheduledSettingKey) ?? new();
        }

        private async Task SaveScheduledAsync(List<NewsLetterScheduledSend> scheduled)
        {
            await SaveJsonSettingAsync(ScheduledSettingKey, scheduled.OrderBy(x => x.DataOraProgrammataUtc).ToList());
        }

        private async Task<T?> LoadJsonSettingAsync<T>(string key)
        {
            var setting = await _dbContext.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);

            if (string.IsNullOrWhiteSpace(setting?.Value))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(setting.Value);
            }
            catch
            {
                return default;
            }
        }

        private async Task SaveJsonSettingAsync<T>(string key, T value)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new AppSetting { Key = key };
                _dbContext.AppSettings.Add(setting);
            }

            setting.Value = JsonSerializer.Serialize(value);
            await _dbContext.SaveChangesAsync();
        }

        private static string NormalizeSegment(string? segment)
        {
            return segment is "currentYear" or "last12Months" ? segment : "all";
        }

        private sealed record NewsletterRecipient(string Email, string UnsubscribeToken);

        private static NewsLetterTemplate BuildDefaultTemplate()
        {
            return new NewsLetterTemplate
            {
                Nome = "Template test",
                Oggetto = "News da Full Metal Paintball Carmagnola",
                Titolo = "Novità dal campo",
                Sopratitolo = "Full Metal Paintball Carmagnola",
                Anteprima = "Una comunicazione di prova per la community Full Metal Paintball Carmagnola.",
                Messaggio = "Ciao!\n\nStiamo preparando nuove comunicazioni per la nostra community paintball.\n\nQuesto è un invio di prova.",
                TestoPulsante = "Scopri di più",
                LinkPulsante = CurrentWebsiteUrl,
                LogoUrl = "https://i.imgur.com/K9Ugseg.gif",
                MostraLogo = true,
                FooterPersonalizzato = "Ricevi questa email perche hai lasciato il consenso durante il tesseramento.",
                ColorePrimario = "#e6007e",
                ColoreSecondario = "#f77f00",
                ColoreSfondo = "#f4f0df",
                ColoreScheda = "#fffaf1"
            };
        }

        private static NewsLetterTemplate NormalizeTemplate(NewsLetterTemplate template)
        {
            template.Id = string.IsNullOrWhiteSpace(template.Id) ? Guid.NewGuid().ToString("N") : template.Id;
            template.Nome = template.Nome.Trim();
            template.Oggetto = template.Oggetto.Trim();
            template.Titolo = template.Titolo.Trim();
            template.Sopratitolo = string.IsNullOrWhiteSpace(template.Sopratitolo) ? "Full Metal Paintball Carmagnola" : template.Sopratitolo.Trim();
            template.Anteprima = string.IsNullOrWhiteSpace(template.Anteprima) ? null : template.Anteprima.Trim();
            template.Messaggio = template.Messaggio.Trim();
            template.TestoPulsante = string.IsNullOrWhiteSpace(template.TestoPulsante) ? null : template.TestoPulsante.Trim();
            template.LinkPulsante = string.IsNullOrWhiteSpace(template.LinkPulsante) ? null : template.LinkPulsante.Trim();
            if (string.Equals(template.LinkPulsante, LegacyWebsiteUrl, StringComparison.OrdinalIgnoreCase))
            {
                template.LinkPulsante = CurrentWebsiteUrl;
            }
            template.ImmagineUrl = string.IsNullOrWhiteSpace(template.ImmagineUrl) ? null : template.ImmagineUrl.Trim();
            template.LogoUrl = string.IsNullOrWhiteSpace(template.LogoUrl) ? "https://i.imgur.com/K9Ugseg.gif" : template.LogoUrl.Trim();
            template.FooterPersonalizzato = string.IsNullOrWhiteSpace(template.FooterPersonalizzato) ? null : template.FooterPersonalizzato.Trim();
            template.ColorePrimario = string.IsNullOrWhiteSpace(template.ColorePrimario) ? "#e6007e" : template.ColorePrimario.Trim();
            template.ColoreSecondario = string.IsNullOrWhiteSpace(template.ColoreSecondario) ? "#f77f00" : template.ColoreSecondario.Trim();
            template.ColoreSfondo = string.IsNullOrWhiteSpace(template.ColoreSfondo) ? "#f4f0df" : template.ColoreSfondo.Trim();
            template.ColoreScheda = string.IsNullOrWhiteSpace(template.ColoreScheda) ? "#fffaf1" : template.ColoreScheda.Trim();
            template.UpdatedAtUtc = DateTime.UtcNow;
            return template;
        }

        private static void ReplaceLegacyWebsiteLinks(IEnumerable<NewsLetterTemplate> templates)
        {
            foreach (var template in templates)
            {
                if (string.Equals(template.LinkPulsante, LegacyWebsiteUrl, StringComparison.OrdinalIgnoreCase))
                {
                    template.LinkPulsante = CurrentWebsiteUrl;
                }
            }
        }

        private static NewsLetterTemplate CloneTemplate(NewsLetterTemplate template)
        {
            return new NewsLetterTemplate
            {
                Id = template.Id,
                Nome = template.Nome,
                Oggetto = template.Oggetto,
                Titolo = template.Titolo,
                Sopratitolo = template.Sopratitolo,
                Anteprima = template.Anteprima,
                Messaggio = template.Messaggio,
                TestoPulsante = template.TestoPulsante,
                LinkPulsante = template.LinkPulsante,
                ImmagineUrl = template.ImmagineUrl,
                LogoUrl = template.LogoUrl,
                MostraLogo = template.MostraLogo,
                FooterPersonalizzato = template.FooterPersonalizzato,
                ColorePrimario = template.ColorePrimario,
                ColoreSecondario = template.ColoreSecondario,
                ColoreSfondo = template.ColoreSfondo,
                ColoreScheda = template.ColoreScheda,
                UpdatedAtUtc = template.UpdatedAtUtc
            };
        }

        private static string BuildTemplateHtml(NewsLetterTemplate template, string? unsubscribeUrl)
        {
            var title = Html(template.Titolo);
            var eyebrow = string.IsNullOrWhiteSpace(template.Sopratitolo)
                ? string.Empty
                : $@"<div style=""color:#fff;font-size:13px;font-weight:800;letter-spacing:.12em;text-transform:uppercase;"">{Html(template.Sopratitolo)}</div>";
            var logo = template.MostraLogo && !string.IsNullOrWhiteSpace(template.LogoUrl)
                ? $@"<img src=""{Attr(template.LogoUrl)}"" alt=""Full Metal Paintball Carmagnola"" style=""width:88px;height:88px;border-radius:999px;background:#fff;padding:8px;display:block;margin:0 auto 16px;"">"
                : string.Empty;
            var preheader = string.IsNullOrWhiteSpace(template.Anteprima)
                ? string.Empty
                : $@"<div style=""display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;"">{Html(template.Anteprima)}</div>";
            var message = Html(template.Messaggio)
                .Replace("\r\n", "\n")
                .Replace("\n", "<br />");
            var image = string.IsNullOrWhiteSpace(template.ImmagineUrl)
                ? string.Empty
                : $@"<tr><td><img src=""{Attr(template.ImmagineUrl)}"" alt="""" style=""width:100%;display:block;max-height:280px;object-fit:cover;""></td></tr>";
            var button = string.IsNullOrWhiteSpace(template.TestoPulsante) || string.IsNullOrWhiteSpace(template.LinkPulsante)
                ? string.Empty
                : $@"<tr><td style=""padding:8px 30px 30px;text-align:center;""><a href=""{Attr(template.LinkPulsante)}"" style=""display:inline-block;background:linear-gradient(135deg,{Attr(template.ColorePrimario)},{Attr(template.ColoreSecondario)});color:#fff;text-decoration:none;border-radius:999px;padding:13px 22px;font-weight:900;"">{Html(template.TestoPulsante)}</a></td></tr>";
            var footerText = string.IsNullOrWhiteSpace(template.FooterPersonalizzato)
                ? "Messaggio inviato da A.S.D. Full Metal Paintball Carmagnola."
                : Html(template.FooterPersonalizzato).Replace("\r\n", "\n").Replace("\n", "<br />");
            var unsubscribe = string.IsNullOrWhiteSpace(unsubscribeUrl)
                ? string.Empty
                : $@"<br><a href=""{Attr(unsubscribeUrl)}"" style=""color:#6d6a5c;text-decoration:underline;"">Disiscriviti dalla newsletter</a>";

            return $@"<!doctype html>
<html lang=""it"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{title}</title>
</head>
<body style=""margin:0;padding:0;background:{Attr(template.ColoreSfondo)};font-family:Arial,Helvetica,sans-serif;color:#141711;"">
  {preheader}
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:{Attr(template.ColoreSfondo)};padding:28px 12px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:{Attr(template.ColoreScheda)};border-radius:28px;overflow:hidden;border:1px solid #eadfbd;box-shadow:0 18px 45px rgba(0,0,0,.10);"">
          <tr>
            <td style=""padding:28px 28px 18px;text-align:center;background:linear-gradient(135deg,{Attr(template.ColorePrimario)},{Attr(template.ColoreSecondario)});"">
              {logo}
              {eyebrow}
              <h1 style=""color:#fff;font-size:30px;line-height:1.12;margin:8px 0 0;font-weight:900;"">{title}</h1>
            </td>
          </tr>
          {image}
          <tr>
            <td style=""padding:30px 30px 18px;font-size:17px;line-height:1.65;"">
              {message}
            </td>
          </tr>
          {button}
          <tr>
            <td style=""padding:18px 30px 30px;"">
              <div style=""border-top:1px solid #eadfbd;padding-top:18px;color:#6d6a5c;font-size:13px;line-height:1.5;"">
                {footerText}{unsubscribe}<br>
                Questa è una versione di test della newsletter.
              </div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static string Attr(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
