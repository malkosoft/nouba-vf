using Microsoft.AspNetCore.Mvc;
using Nouba.Services;

namespace Nouba.Controllers;

[Route("Diagnostics")]
public class DiagnosticsController : Controller
{
    private readonly PiperTtsService _tts;
    private readonly EscPosPrinter _printer;
    private readonly IWebHostEnvironment _env;
    private const string SessionKey = "AdminUserId";
    private const string SessionRoleKey = "AdminRole";

    public DiagnosticsController(PiperTtsService tts, EscPosPrinter printer, IWebHostEnvironment env)
    {
        _tts = tts;
        _printer = printer;
        _env = env;
    }

    private bool IsAdmin() =>
        HttpContext.Session.GetInt32(SessionKey).HasValue &&
        string.Equals(HttpContext.Session.GetString(SessionRoleKey), "fournisseur", StringComparison.OrdinalIgnoreCase);

    [HttpGet("Status")]
    public IActionResult Status()
    {
        if (!IsAdmin()) return Unauthorized();
        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath) ? Path.Combine(_env.ContentRootPath, "wwwroot") : _env.WebRootPath;
        var piperDir = Path.Combine(webRoot, "tts", "piper");
        return Json(new
        {
            system = new
            {
                ready = Directory.Exists(webRoot) && Directory.Exists(piperDir),
                webRoot = Directory.Exists(webRoot),
                piperFolder = Directory.Exists(piperDir)
            },
            tts = _tts.GetStatus(),
            printers = new
            {
                installed = _printer.ListInstalledPrinters(),
                serialPorts = _printer.ListSerialPorts()
            },
            display = new { url = Url.Action("Index", "Display") ?? "/Display" },
            borne = new { url = Url.Action("Index", "Borne") ?? "/Borne" }
        });
    }
}
