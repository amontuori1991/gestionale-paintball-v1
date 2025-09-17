using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[AllowAnonymous]
public class SurveyPublicController : Controller
{
    private readonly TesseramentoDbContext _db;

    public SurveyPublicController(TesseramentoDbContext db)
    {
        _db = db;
    }

    [HttpGet("/Survey/{slug}")]
    public async Task<IActionResult> Public(string slug)
    {
        var survey = await _db.Surveys
            .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(s => s.PublicSlug == slug && s.IsActive);

        if (survey == null) return NotFound("Sondaggio non disponibile.");

        return View(survey);
    }

    [HttpPost("/Survey/{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string slug, [FromForm] Dictionary<string, string[]> formAnswers)
    {
        var survey = await _db.Surveys
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.PublicSlug == slug && s.IsActive);

        if (survey == null) return NotFound("Sondaggio non disponibile.");

        var response = new SurveyResponse
        {
            SurveyId = survey.Id,
            SubmittedAt = DateTime.UtcNow,
            SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
        _db.SurveyResponses.Add(response);
        await _db.SaveChangesAsync();

        // Parsing risposte: name="q_{questionId}" (testo) o "q_{questionId}_opt" (opzioni)
        foreach (var q in survey.Questions)
        {
            // testo libero
            var keyText = $"q_{q.Id}";
            if (q.Type == QuestionType.OpenText && Request.Form.ContainsKey(keyText))
            {
                var val = Request.Form[keyText].ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    _db.SurveyAnswers.Add(new SurveyAnswer
                    {
                        ResponseId = response.Id,
                        QuestionId = q.Id,
                        AnswerText = val
                    });
                }
                continue;
            }

            // multiple choice
            var keyOpt = $"q_{q.Id}_opt";
            if (Request.Form.ContainsKey(keyOpt))
            {
                var optionIds = Request.Form[keyOpt].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var raw in optionIds)
                {
                    if (int.TryParse(raw, out int optId))
                    {
                        _db.SurveyAnswers.Add(new SurveyAnswer
                        {
                            ResponseId = response.Id,
                            QuestionId = q.Id,
                            OptionId = optId
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Thanks));
    }

    [HttpGet("/Survey/Thanks")]
    public IActionResult Thanks() => View();
}
