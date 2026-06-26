using Microsoft.AspNetCore.Mvc;
using Nouba.Services;

namespace Nouba.Controllers;

/// <summary>
/// Endpoints IA — résumé quotidien, rapport pro, traduction.
/// Tous offline. (Le chatbot d'orientation a été retiré en v2.7.)
/// </summary>
[Route("Ai")]
public class AiController : Controller
{
    private readonly NoubaAiService _ai;
    private const string AdminSessionKey = "AdminUserId";
    public AiController(NoubaAiService ai) { _ai = ai; }

    private bool IsAdmin() => HttpContext.Session.GetInt32(AdminSessionKey).HasValue;

    /// <summary>Résumé du jour (JSON). Public — utile pour widget admin et pour la borne.</summary>
    [HttpGet("Summary")]
    public async Task<IActionResult> Summary(DateTime? date)
    {
        if (!IsAdmin()) return Unauthorized();
        var s = await _ai.BuildDailySummaryAsync(date, HttpContext.RequestAborted);
        return Json(s);
    }

    /// <summary>Rapport textuel pro (texte brut, prêt à coller dans un email).</summary>
    [HttpGet("Report")]
    public async Task<IActionResult> Report(DateTime? date)
    {
        if (!IsAdmin()) return Unauthorized();
        var txt = await _ai.GenerateProfessionalReportAsync(date, HttpContext.RequestAborted);
        return Content(txt, "text/plain; charset=utf-8");
    }

    /// <summary>Traduction du nom d'un service (FR → AR/TZ/EN). Réservé à l'admin.</summary>
    [HttpGet("Translate")]
    public IActionResult Translate(string nameFr)
    {
        if (!IsAdmin()) return Unauthorized();
        return Json(_ai.TranslateServiceName(nameFr ?? ""));
    }
}
