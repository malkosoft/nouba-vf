using Microsoft.AspNetCore.Http;
using Nouba.Models;

namespace Nouba.Helpers;

/// <summary>
/// Construit l'URL publique de suivi d'un ticket.
/// Si QrFollowPublicUrl est configurée (ex: https://suivi.nouba.app),
/// on l'utilise. Sinon on retombe sur l'URL où Nouba tourne (LAN local) :
/// utile pour démarrer sans déployer le serveur public.
/// </summary>
public static class TicketTrackingUrl
{
    /// <param name="tunnelUrl">URL du tunnel Cloudflare actif (CloudflareTunnelService.TunnelUrl).
    /// Utilisée comme fallback quand QrFollowPublicUrl n'est pas configuré.</param>
    public static string Build(UiSettings settings, string publicId, HttpRequest? currentRequest = null, string? tunnelUrl = null)
    {
        var publicRoot = (settings.QrFollowPublicUrl ?? "").Trim().TrimEnd('/');

        // Priorité 1 : URL publique configurée manuellement par l'admin.
        if (!string.IsNullOrEmpty(publicRoot))
            return $"{publicRoot}/suivi/{publicId}";

        // Priorité 2 : tunnel Cloudflare actif (automatique si cloudflared.exe présent).
        if (!string.IsNullOrEmpty(tunnelUrl))
            return $"{tunnelUrl.TrimEnd('/')}/suivi/{publicId}";

        // Priorité 3 : URL locale (Wi-Fi interne, démos).
        if (currentRequest != null)
        {
            // Si l'opérateur a ouvert Nouba via localhost/127.0.0.1/0.0.0.0, l'hôte
            // de la requête n'est PAS joignable depuis le téléphone du client. On le
            // remplace alors par l'IP LAN réelle du PC (ex: 192.168.1.50) + le port.
            if (NetworkHelper.IsLoopbackOrAny(currentRequest.Host.Host))
            {
                var lanIp = NetworkHelper.GetLanIp();
                if (!string.IsNullOrEmpty(lanIp))
                {
                    var port = currentRequest.Host.Port ?? 5000;
                    return $"http://{lanIp}:{port}/suivi/{publicId}";
                }
            }
            return $"{currentRequest.Scheme}://{currentRequest.Host.Value}/suivi/{publicId}";
        }

        return $"/suivi/{publicId}";
    }
}
