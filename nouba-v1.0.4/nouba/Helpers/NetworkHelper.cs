using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Nouba.Helpers;

/// <summary>
/// Détection de l'adresse IPv4 LAN « joignable » du PC (ex: 192.168.1.50),
/// utilisée pour construire les URL de suivi QR quand l'opérateur a ouvert
/// Nouba via localhost (auquel cas un téléphone ne pourrait pas joindre le PC).
/// </summary>
public static class NetworkHelper
{
    public static string? GetLanIp()
    {
        try
        {
            var candidates = new List<(string Ip, int Score)>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var desc = (ni.Description + " " + ni.Name).ToLowerInvariant();
                // On écarte les adaptateurs virtuels (VMware/VirtualBox/Hyper-V/WSL)
                // qui exposent une IP privée NON joignable par le téléphone.
                bool virtualAdapter = desc.Contains("virtual") || desc.Contains("vmware")
                    || desc.Contains("vbox") || desc.Contains("hyper-v") || desc.Contains("wsl")
                    || desc.Contains("loopback");
                bool physical = ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                    or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                    or NetworkInterfaceType.Wireless80211;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue; // APIPA
                    if (ip == "127.0.0.1") continue;

                    int score = 0;
                    if (ip.StartsWith("192.168.")) score += 100;      // réseaux domestiques/bureaux
                    else if (ip.StartsWith("10.")) score += 80;        // entreprises
                    else if (IsPrivate172(ip)) score += 60;            // 172.16-31.x
                    else score += 20;                                  // autre (IP publique directe rare)
                    if (physical) score += 10;
                    if (virtualAdapter) score -= 200;                  // jamais un adaptateur virtuel si évitable
                    candidates.Add((ip, score));
                }
            }
            return candidates.OrderByDescending(c => c.Score).Select(c => c.Ip).FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Vrai si l'hôte n'est pas joignable depuis un autre appareil (localhost/0.0.0.0).</summary>
    public static bool IsLoopbackOrAny(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return true;
        host = host.Trim().ToLowerInvariant();
        return host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1" or "[::1]";
    }

    private static bool IsPrivate172(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length == 4 && parts[0] == "172"
            && int.TryParse(parts[1], out var b) && b >= 16 && b <= 31;
    }
}
