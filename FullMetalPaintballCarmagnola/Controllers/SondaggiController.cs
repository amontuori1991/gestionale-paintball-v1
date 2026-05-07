using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = "Sondaggi")]
public class SondaggiController : Controller
{
    private readonly TesseramentoDbContext _db;

    public SondaggiController(TesseramentoDbContext db)
    {
        _db = db;
    }

    // Elenco (attivi e bozze)
    public async Task<IActionResult> Index()
    {
        var items = await _db.Surveys
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return View(items);
    }

    // Crea
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Survey { PublicSlug = GenerateSlug() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Survey model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;

        _db.Surveys.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    // Modifica (builder)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var survey = await _db.Surveys
            .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (survey == null) return NotFound();
        return View(survey);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Survey model)
    {
        var survey = await _db.Surveys.FindAsync(id);
        if (survey == null) return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        survey.Title = model.Title;
        survey.Description = model.Description;
        survey.IsActive = model.IsActive;
        survey.PublicSlug = model.PublicSlug;
        survey.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Aggiungi domanda
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(int surveyId, string text, QuestionType type, bool allowMultiple)
    {
        var maxOrder = await _db.SurveyQuestions
            .Where(q => q.SurveyId == surveyId)
            .MaxAsync(q => (int?)q.Order) ?? -1;

        var qn = new SurveyQuestion
        {
            SurveyId = surveyId,
            Text = text,
            Type = type,
            AllowMultiple = allowMultiple,
            Order = maxOrder + 1
        };
        _db.SurveyQuestions.Add(qn);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = surveyId });
    }

    // Rimuovi domanda
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveQuestion(int id)
    {
        var qn = await _db.SurveyQuestions.FindAsync(id);
        if (qn == null) return NotFound();
        var surveyId = qn.SurveyId;
        _db.SurveyQuestions.Remove(qn);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = surveyId });
    }

    // Aggiungi opzione
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOption(int questionId, string text)
    {
        var maxOrder = await _db.SurveyOptions
            .Where(o => o.QuestionId == questionId)
            .MaxAsync(o => (int?)o.Order) ?? -1;

        var opt = new SurveyOption
        {
            QuestionId = questionId,
            Text = text,
            Order = maxOrder + 1
        };
        _db.SurveyOptions.Add(opt);
        await _db.SaveChangesAsync();

        var surveyId = await _db.SurveyQuestions.Where(q => q.Id == questionId).Select(q => q.SurveyId).FirstAsync();
        return RedirectToAction(nameof(Edit), new { id = surveyId });
    }

    // Rimuovi opzione
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOption(int id)
    {
        var opt = await _db.SurveyOptions.FindAsync(id);
        if (opt == null) return NotFound();
        var surveyId = await _db.SurveyQuestions.Where(q => q.Id == opt.QuestionId).Select(q => q.SurveyId).FirstAsync();
        _db.SurveyOptions.Remove(opt);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = surveyId });
    }

    // Attiva/Disattiva sondaggio
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var s = await _db.Surveys.FindAsync(id);
        if (s == null) return NotFound();
        s.IsActive = !s.IsActive;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // QR + link
    [HttpGet]
    public async Task<IActionResult> Qr(int id)
    {
        var s = await _db.Surveys.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();

        var publicUrl = Url.Action("Public", "SurveyPublic", new { slug = s.PublicSlug }, Request.Scheme);
        ViewBag.PublicUrl = publicUrl;

        return View(s);
    }


    // Risposte + export
    [HttpGet]
    public async Task<IActionResult> Responses(int id)
    {
        var s = await _db.Surveys.FindAsync(id);
        if (s == null) return NotFound();

        var questions = await _db.SurveyQuestions
            .Where(q => q.SurveyId == id)
            .OrderBy(q => q.Order)
            .ToListAsync();

        var responses = await _db.SurveyResponses
            .Where(r => r.SurveyId == id)
            .OrderByDescending(r => r.SubmittedAt)
            .Include(r => r.Answers)
            .ToListAsync();

        var optionIds = responses
            .SelectMany(r => r.Answers)
            .Where(a => a.OptionId.HasValue)
            .Select(a => a.OptionId!.Value)
            .Distinct()
            .ToList();

        var optionMap = await _db.SurveyOptions
            .Where(o => optionIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Text);

        ViewBag.Questions = questions;
        ViewBag.OptionMap = optionMap;

        // Passo IEnumerable per evitare mismatch List/IEnumerable
        return View((s, responses.AsEnumerable()));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuestion(int id, string text, int type, bool allowMultiple)
    {
        var q = await _db.SurveyQuestions.FindAsync(id);
        if (q == null) return NotFound();
        q.Text = text;
        q.Type = (QuestionType)type;
        q.AllowMultiple = allowMultiple;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = q.SurveyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOption(int id, string text)
    {
        var opt = await _db.SurveyOptions.FindAsync(id);
        if (opt == null) return NotFound();
        opt.Text = text;
        await _db.SaveChangesAsync();
        var surveyId = await _db.SurveyQuestions.Where(q => q.Id == opt.QuestionId).Select(q => q.SurveyId).FirstAsync();
        return RedirectToAction(nameof(Edit), new { id = surveyId });
    }


    [HttpGet]
    public async Task<FileResult> ExportCsv(int id)
    {
        var survey = await _db.Surveys.FindAsync(id);
        if (survey == null) throw new Exception("Survey not found.");

        var questions = await _db.SurveyQuestions.Where(q => q.SurveyId == id).OrderBy(q => q.Order).ToListAsync();
        var responses = await _db.SurveyResponses
            .Where(r => r.SurveyId == id)
            .Include(r => r.Answers)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        // header
        sb.Append("ResponseId;SubmittedAt;");
        foreach (var q in questions) sb.Append(Escape(q.Text)).Append(';');
        sb.Length--; sb.AppendLine();

        foreach (var r in responses)
        {
            sb.Append(r.Id).Append(';')
              .Append(r.SubmittedAt.ToString("s")).Append(';');

            foreach (var q in questions)
            {
                var answers = r.Answers.Where(a => a.QuestionId == q.Id).ToList();
                string cell = answers.Count == 0 ? "" :
                              string.Join(" | ", answers.Select(a => a.OptionId.HasValue ? GetOptionText(a.OptionId.Value) : a.AnswerText));
                sb.Append(Escape(cell)).Append(';');
            }
            sb.Length--; sb.AppendLine();
        }

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{SanitizeFileName(survey.Title)}_responses.csv");

        string Escape(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        string GetOptionText(int optionId) => _db.SurveyOptions.Find(optionId)?.Text ?? $"Option#{optionId}";
        string SanitizeFileName(string s) => string.Concat(s.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
    }

    private static string GenerateSlug()
    {
        var guid = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "").Replace("/", "").Replace("=", "");
        return guid[..8].ToLower();
    }
}

// Helper QR (senza dipendenze esterne, usa System.Drawing)
static class QrHelper
{
    // Semplice fallback: usa API chart.googleapis come PNG data URI
    // (stabile, nessuna chiave, nessun pacchetto). In caso tu voglia QRCoder,
    // posso darti il service dedicato.
    public static string GeneratePngDataUri(string text)
    {
        // Google Chart QR API
        var url = $"https://chart.googleapis.com/chart?cht=qr&chs=400x400&chl={Uri.EscapeDataString(text)}";
        using var http = new HttpClient();
        var data = http.GetByteArrayAsync(url).Result;
        var base64 = Convert.ToBase64String(data);
        return $"data:image/png;base64,{base64}";
    }
}
