using Microsoft.AspNetCore.Mvc;

namespace Nouba.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult SetLanguage(string lang, string? returnUrl = null, bool json = false)
    {
        lang = string.IsNullOrWhiteSpace(lang) ? "fr" : lang.Trim().ToLowerInvariant();
        if (lang is not ("fr" or "ar" or "tz" or "en")) lang = "fr";

        Response.Cookies.Append("nouba_lang", lang, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        });

        if (json)
            return Json(new { ok = true, lang });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Borne");
    }

    public IActionResult Error()
    {
        return View();
    }
}
