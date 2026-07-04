using Microsoft.AspNetCore.Diagnostics;

namespace Nouba.Infrastructure;

/// <summary>
/// Garde « disque plein » (plan P3). Intercepte les écritures impossibles faute
/// d'espace (SQLite <c>SQLITE_FULL</c>, IOException disque plein) et rend une page
/// claire au lieu d'une erreur 500 opaque — typiquement sur <c>/Admin/Login</c>.
///
/// La réponse est écrite « à la main » (pas de vue MVC, pas d'accès base) pour
/// rester fiable justement quand le disque est plein. On désactive aussi la
/// ré-exécution StatusCodePages (qui, en repassant par /Home/Error, retoucherait
/// le disque et provoquerait une double faute).
/// </summary>
public sealed class DiskGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DiskGuardMiddleware> _logger;
    private readonly AppStoragePaths _paths;

    public DiskGuardMiddleware(RequestDelegate next, ILogger<DiskGuardMiddleware> logger, AppStoragePaths paths)
    {
        _next = next;
        _logger = logger;
        _paths = paths;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex) when (DiskFull.IsDiskFull(ex))
        {
            await WriteDiskFullPageAsync(ctx, ex);
        }
    }

    private async Task WriteDiskFullPageAsync(HttpContext ctx, Exception ex)
    {
        var freeMb = DiskFull.FreeMb(_paths.DataRoot);
        _logger.LogError(ex,
            "Espace disque insuffisant : écriture impossible (disque plein). Espace libre ~{FreeMb} Mo sur {Root}.",
            freeMb, _paths.DataRoot);

        // Empêcher StatusCodePages de remplacer NOTRE page par /Home/Error.
        var scp = ctx.Features.Get<IStatusCodePagesFeature>();
        if (scp is not null) scp.Enabled = false;

        if (ctx.Response.HasStarted)
        {
            // La réponse est déjà partie : on ne peut plus rien réécrire proprement.
            _logger.LogWarning("Disque plein détecté mais la réponse avait déjà commencé — page d'erreur non rendue.");
            return;
        }

        ctx.Response.Clear();
        ctx.Response.StatusCode = StatusCodes.Status507InsufficientStorage;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        await ctx.Response.WriteAsync(BuildHtml(freeMb));
    }

    private static string BuildHtml(long freeMb)
    {
        var free = freeMb >= 0 ? $"{freeMb} Mo" : "—";
        return $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
<meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Espace disque insuffisant — Nouba Pro</title>
<style>
  body{{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
       font-family:'Segoe UI',Tahoma,Arial,sans-serif;background:#0b1220;color:#e5e7eb;padding:24px}}
  .card{{max-width:560px;background:#111a2e;border:1px solid #24314d;border-radius:18px;
        padding:34px 36px;box-shadow:0 20px 60px rgba(0,0,0,.45)}}
  h1{{font-size:22px;margin:0 0 6px;color:#f3d486}}
  .icon{{font-size:42px;line-height:1}}
  p{{line-height:1.6;margin:12px 0;color:#cbd5e1}}
  .steps{{margin:14px 0 4px;padding-inline-start:20px}} .steps li{{margin:6px 0}}
  code{{background:#0b1220;border:1px solid #24314d;border-radius:6px;padding:2px 7px;color:#93c5fd}}
  .free{{font-weight:700;color:#fca5a5}}
  .ar{{direction:rtl;text-align:right;border-top:1px solid #24314d;margin-top:20px;padding-top:16px;font-size:.95em}}
  .retry{{display:inline-block;margin-top:16px;background:#0f766e;color:#fff;text-decoration:none;
          padding:11px 20px;border-radius:10px;font-weight:700}}
</style>
</head>
<body>
  <div class=""card"">
    <div class=""icon"">💽</div>
    <h1>Espace disque insuffisant</h1>
    <p>Nouba n'a pas pu enregistrer les données car le disque est <b>plein</b>
       (espace libre&nbsp;: <span class=""free"">{free}</span>). Vos données existantes
       ne sont <b>pas perdues</b>.</p>
    <p>Pour rétablir le service&nbsp;:</p>
    <ol class=""steps"">
      <li>Libérez de l'espace sur le disque (corbeille, fichiers temporaires, anciennes vidéos).</li>
      <li>Vous pouvez déplacer d'anciennes sauvegardes hors de <code>C:\ProgramData\Nouba\backups</code>.</li>
      <li>Réessayez ensuite l'opération.</li>
    </ol>
    <a class=""retry"" href=""javascript:history.back()"">Réessayer</a>

    <div class=""ar"">
      <h1 style=""color:#f3d486;font-size:20px"">مساحة القرص غير كافية</h1>
      <p>تعذّر على نوبة حفظ البيانات لأن القرص <b>ممتلئ</b> (المساحة المتاحة:
         <span class=""free"">{free}</span>). بياناتك الحالية <b>غير مفقودة</b>.
         حرّر بعض المساحة على القرص ثم أعد المحاولة.</p>
    </div>
  </div>
</body>
</html>";
    }
}
