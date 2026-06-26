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
    public static string Build(UiSettings settings, string publicId, HttpRequest? currentRequest = null)
    {
        var publicRoot = (settings.QrFollowPublicUrl ?? "").Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(publicRoot))
        {
            return $"{publicRoot}/suivi/{publicId}";
        }
        // Fallback : URL locale (utile pour Wi-Fi, démos, ou tant que le
        // serveur public n'est pas déployé).
        if (currentRequest != null)
        {
            var scheme = currentRequest.Scheme;
            var host = currentRequest.Host.Value;
            return $"{scheme}://{host}/suivi/{publicId}";
        }
        // Dernier fallback : chemin relatif (le navigateur résoudra contre l'origin).
        return $"/suivi/{publicId}";
    }
}
