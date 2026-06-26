using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nouba.Infrastructure;

namespace Nouba.Controllers;

/// <summary>
/// Gestion de l'activation et de la validation de licence côté client.
/// Important : aucun générateur de licence n'est exposé dans l'application client.
/// La création des clés se fait exclusivement avec l'outil vendeur séparé,
/// hors du logiciel livré.
/// </summary>
public class LicenseController : Controller
{
    private readonly AppStoragePaths _paths;
    private readonly IConfiguration _configuration;

    public LicenseController(AppStoragePaths paths, IConfiguration configuration)
    {
        _paths = paths;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Activate()
    {
        var status = LicenseManager.CheckLicense(_paths);
        if (status.IsValid)
            return RedirectToAction("Index", "Borne");

        ViewBag.MachineId = status.MachineId;
        ViewBag.PrimaryMac = status.PrimaryMac ?? "Non détectée";
        ViewBag.Message   = TempData["LicError"] as string ?? string.Empty;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            TempData["LicError"] = "Veuillez saisir une clé de licence ou un code d'activation.";
            return RedirectToAction(nameof(Activate));
        }

        var submitted = licenseKey.Trim();

        // Mode hybride : si le vendeur configure un serveur d'activation, le client peut saisir
        // un code court ONLINE:<code>. Le serveur retourne alors une licence RSA offline signée.
        if (submitted.StartsWith("ONLINE:", StringComparison.OrdinalIgnoreCase))
        {
            var onlineLicense = await TryActivateOnlineAsync(submitted[7..].Trim());
            if (string.IsNullOrWhiteSpace(onlineLicense))
                return RedirectToAction(nameof(Activate));

            submitted = onlineLicense;
        }

        var result = LicenseManager.ValidateLicense(submitted);
        if (!result.IsValid)
        {
            TempData["LicError"] = result.Message;
            return RedirectToAction(nameof(Activate));
        }

        LicenseManager.SaveLicense(_paths, submitted);
        return RedirectToAction("Index", "Borne");
    }

    private async Task<string?> TryActivateOnlineAsync(string activationCode)
    {
        var endpoint = _configuration["Nouba:LicenseServer:ActivationUrl"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            TempData["LicError"] = "Activation en ligne non configurée sur cette installation.";
            return null;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var response = await http.PostAsJsonAsync(endpoint, new
            {
                product = "Nouba Pro",
                activationCode,
                machineId = LicenseManager.GetMachineId(),
                primaryMac = LicenseManager.GetPrimaryMacAddress(),
                version = LicenseInfo.FullVersion
            });

            if (!response.IsSuccessStatusCode)
            {
                TempData["LicError"] = "Activation en ligne refusée par le serveur.";
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<OnlineActivationResponse>();
            if (string.IsNullOrWhiteSpace(payload?.LicenseKey))
            {
                TempData["LicError"] = "Réponse d'activation en ligne invalide.";
                return null;
            }

            return payload.LicenseKey;
        }
        catch
        {
            TempData["LicError"] = "Activation en ligne indisponible. Utilisez une licence offline.";
            return null;
        }
    }

    private sealed class OnlineActivationResponse
    {
        public string? LicenseKey { get; set; }
    }
}
