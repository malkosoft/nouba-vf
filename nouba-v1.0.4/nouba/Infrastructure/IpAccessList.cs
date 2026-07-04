using System.Net;

namespace Nouba.Infrastructure;

/// <summary>
/// Contrôle d'accès par IP pour l'administration (localhost + liste blanche LAN).
/// Le poste hôte (loopback 127.0.0.1 / ::1) est TOUJOURS autorisé : l'administrateur
/// ne peut donc jamais se verrouiller dehors depuis le mini-PC qui héberge Nouba,
/// même si la liste blanche est vide ou mal renseignée.
/// </summary>
public static class IpAccessList
{
    private static readonly char[] Separators = { ',', ';', '\n', '\r', ' ', '\t' };

    /// <summary>
    /// Vrai si <paramref name="remote"/> est le loopback OU figure dans
    /// <paramref name="allowList"/> (adresses IP exactes ou plages CIDR).
    /// </summary>
    public static bool IsAllowed(IPAddress? remote, string? allowList)
    {
        if (remote is null) return false;
        var ip = Normalize(remote);
        if (IPAddress.IsLoopback(ip)) return true;
        if (string.IsNullOrWhiteSpace(allowList)) return false;

        foreach (var raw in allowList.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = raw.Trim();
            if (entry.Length == 0) continue;

            var slash = entry.IndexOf('/');
            if (slash > 0)
            {
                if (MatchCidr(ip, entry, slash)) return true;
            }
            else if (IPAddress.TryParse(entry, out var single) && Normalize(single).Equals(ip))
            {
                return true;
            }
        }
        return false;
    }

    private static IPAddress Normalize(IPAddress ip)
        => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static bool MatchCidr(IPAddress ip, string entry, int slash)
    {
        var basePart = entry.Substring(0, slash);
        var prefixPart = entry.Substring(slash + 1);
        if (!IPAddress.TryParse(basePart, out var baseIp)) return false;
        if (!int.TryParse(prefixPart, out var prefix) || prefix < 0) return false;

        baseIp = Normalize(baseIp);
        if (baseIp.AddressFamily != ip.AddressFamily) return false;

        var ipBytes = ip.GetAddressBytes();
        var baseBytes = baseIp.GetAddressBytes();
        if (ipBytes.Length != baseBytes.Length) return false;

        if (prefix > ipBytes.Length * 8) return false;

        int fullBytes = prefix / 8;
        int remBits = prefix % 8;
        for (int i = 0; i < fullBytes; i++)
            if (ipBytes[i] != baseBytes[i]) return false;
        if (remBits > 0)
        {
            int mask = 0xFF << (8 - remBits);
            if ((ipBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask)) return false;
        }
        return true;
    }
}
