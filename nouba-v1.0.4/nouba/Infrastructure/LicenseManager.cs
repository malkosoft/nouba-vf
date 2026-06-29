using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Nouba.Infrastructure;

/// <summary>
/// Gestion de licence Nouba Pro v2.
///
/// Principe professionnel : le logiciel client contient uniquement la CLE PUBLIQUE RSA.
/// Le generateur vendeur signe les licences avec la CLE PRIVEE RSA, absente du logiciel client.
///
/// Format offline : NOUBA2-&lt;base64url(payload-json)&gt;.&lt;base64url(signature-rsa-sha256)&gt;
/// Le payload contient : machineId, expiration, nombre maximum d'agents actifs, client, id.
/// </summary>
public static class LicenseManager
{
    private const string LicenseFileName = "nouba.lic";
    private const string LicensePrefix = "NOUBA2-";
    private const string ProductName = "Nouba Pro";

    private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAsuWEfGOoJvS0Wq6ABrmU
rlEhoyWKJcuZWC9NMlWj0IVHDUfXNFiqEph1sTs6ZNGdkYnRhPTUGPczr4CSeRie
EDFfub2R1RL4VAaM7Nxbcwi7cnaj9SFE5j/AiIewoeJ0qOZSAPsH/iuSZHmDuFQn
GZCaQYU/LNBv5ubQi640zNJb1KNyAP2GA5S1vrWAAdYroRnepuujONRbFEnYQrGh
ccfb7D5bNrNm5I3I1HsanDzeHyAk05j8U4DTkIIo/yJi0PPK9GoeELTd8XuUgj3r
x7iBH0FcJb6JKdAuPwGK+J0PlAbrx1jy1rHyRlIJX3UvPwApnn14c2v+1bILiJKr
1QIDAQAB
-----END PUBLIC KEY-----";

    private static readonly Regex NonHex = new("[^A-Fa-f0-9]", RegexOptions.Compiled);
    private static readonly string[] VirtualAdapterMarkers =
    [
        "virtual", "vmware", "virtualbox", "hyper-v", "vbox", "docker", "wsl", "vpn",
        "tap", "tun", "loopback", "pseudo", "bluetooth", "teredo", "isatap", "npcap"
    ];

    public static string GetMachineId()
    {
        var mac = GetPrimaryMacAddress();
        var raw = mac ?? Environment.MachineName.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw + "|NOUBA-PRO-MACHINE-V2"));
        return Convert.ToHexString(hash)[..16].ToUpperInvariant();
    }

    public static string? GetPrimaryMacAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(n => new
                {
                    Nic = n,
                    Mac = NormalizeMac(n.GetPhysicalAddress().ToString()),
                    Text = $"{n.Name} {n.Description}".ToLowerInvariant()
                })
                .Where(x => x.Mac.Length == 12 && x.Mac != "000000000000")
                .Where(x => x.Nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(x => x.Nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Where(x => !VirtualAdapterMarkers.Any(marker => x.Text.Contains(marker)))
                .OrderByDescending(x => x.Nic.OperationalStatus == OperationalStatus.Up ? 1 : 0)
                .ThenByDescending(x => IsPreferredPhysicalType(x.Nic.NetworkInterfaceType) ? 1 : 0)
                .ThenByDescending(x => x.Nic.Speed)
                .ThenBy(x => x.Mac, StringComparer.Ordinal)
                .Select(x => x.Mac)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static LicenseValidationResult ValidateLicense(string licenseKey)
    {
        var currentMachineId = GetMachineId();
        if (string.IsNullOrWhiteSpace(licenseKey))
            return LicenseValidationResult.Invalid("Licence vide.", currentMachineId);

        var token = licenseKey.Trim();
        if (!token.StartsWith(LicensePrefix, StringComparison.OrdinalIgnoreCase))
            return LicenseValidationResult.Invalid("Format de licence non reconnu. Une licence RSA v2 est requise.", currentMachineId);

        var compact = token[LicensePrefix.Length..].Trim();
        var parts = compact.Split('.', 2);
        if (parts.Length != 2)
            return LicenseValidationResult.Invalid("Format de licence incomplet.", currentMachineId);

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var signature = Base64UrlDecode(parts[1]);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);
            var validSignature = rsa.VerifyData(
                Encoding.UTF8.GetBytes(parts[0]),
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!validSignature)
                return LicenseValidationResult.Invalid("Signature RSA invalide.", currentMachineId);

            var payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, JsonOptions());
            if (payload is null)
                return LicenseValidationResult.Invalid("Payload de licence illisible.", currentMachineId);

            if (payload.Version != 2)
                return LicenseValidationResult.Invalid("Version de licence non supportée.", currentMachineId, payload);

            if (!string.Equals(payload.Product, ProductName, StringComparison.OrdinalIgnoreCase))
                return LicenseValidationResult.Invalid("Licence destinée à un autre produit.", currentMachineId, payload);

            if (!string.Equals(NormalizeMachineId(payload.MachineId), currentMachineId, StringComparison.OrdinalIgnoreCase))
                return LicenseValidationResult.Invalid("Licence valide mais liée à une autre machine.", currentMachineId, payload);

            if (payload.MaxAgents < 1)
                return LicenseValidationResult.Invalid("Nombre d'agents autorisés invalide.", currentMachineId, payload);

            var today = DateTimeOffset.UtcNow.Date;
            var expiry = payload.ExpiresAt.UtcDateTime.Date;
            if (expiry < today)
                return LicenseValidationResult.Invalid($"Licence expirée le {payload.ExpiresAt:yyyy-MM-dd}.", currentMachineId, payload);

            return LicenseValidationResult.Valid("Licence RSA valide.", currentMachineId, payload);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or ArgumentException)
        {
            return LicenseValidationResult.Invalid("Licence invalide ou corrompue.", currentMachineId);
        }
    }

    public static bool ValidateLicenseKeyOnly(string licenseKey) => ValidateLicense(licenseKey).IsValid;

    public static string GetLicensePath(AppStoragePaths paths)
        => Path.Combine(paths.DataRoot, LicenseFileName);

    // ── Période d'essai gratuite (7 jours) ───────────────────────────────
    // Sur une installation neuve sans licence activée, le logiciel fonctionne
    // 7 jours, puis demande l'activation. La date de 1er lancement est signée
    // (HMAC) pour empêcher de la modifier afin de prolonger l'essai.
    public const int TrialDays = 7;
    private const string TrialFileName = "trial.dat";
    private const string TrialSecret = "NoubaPro|essai|v1|7j|s3cr3t-8f3a2c1d-anti-edit";

    public static LicenseStatus CheckLicense(AppStoragePaths paths)
    {
        var licPath = GetLicensePath(paths);
        var machineId = GetMachineId();
        var primaryMac = GetPrimaryMacAddress();

        if (File.Exists(licPath))
        {
            var stored = File.ReadAllText(licPath).Trim();
            var result = ValidateLicense(stored);
            if (result.IsValid)
                return new LicenseStatus(true, result.Message, machineId, stored, primaryMac, result.Payload);
            // Licence présente mais invalide (expirée / autre machine) :
            // on N'ouvre PAS l'essai (anti-contournement).
            return new LicenseStatus(false, result.Message, machineId, null, primaryMac, result.Payload);
        }

        // Pas de licence activée → on bascule sur la période d'essai.
        return CheckTrial(paths, machineId, primaryMac);
    }

    private static LicenseStatus CheckTrial(AppStoragePaths paths, string machineId, string? primaryMac)
    {
        var firstRunUtc = DateTime.UtcNow;
        var path = Path.Combine(paths.DataRoot, TrialFileName);
        try
        {
            if (File.Exists(path))
            {
                var raw = File.ReadAllText(path).Trim();
                var parts = raw.Split('|');
                if (parts.Length == 2
                    && string.Equals(TrialSign(parts[0]), parts[1], StringComparison.OrdinalIgnoreCase)
                    && DateTime.TryParse(parts[0], CultureInfo.InvariantCulture,
                           DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var stored))
                {
                    firstRunUtc = stored;
                }
                else
                {
                    // Fichier d'essai trafiqué → essai considéré comme terminé.
                    return new LicenseStatus(false, "Période d'essai invalide.", machineId, null, primaryMac, null);
                }
            }
            else
            {
                paths.EnsureCreated();
                WriteTrial(path, firstRunUtc);
            }
        }
        catch { /* accès fichier impossible : on tolère l'essai */ }

        var daysElapsed = (int)Math.Floor((DateTime.UtcNow.Date - firstRunUtc.Date).TotalDays);
        var remaining = TrialDays - daysElapsed;
        if (remaining >= 1)
            return new LicenseStatus(true, $"Version d'essai — {remaining} jour(s) restant(s).",
                machineId, null, primaryMac, null) { IsTrial = true, TrialDaysRemaining = remaining };

        return new LicenseStatus(false, $"Période d'essai de {TrialDays} jours expirée.",
            machineId, null, primaryMac, null) { IsTrial = false, TrialDaysRemaining = 0 };
    }

    private static void WriteTrial(string path, DateTime firstRunUtc)
    {
        try
        {
            var iso = firstRunUtc.ToString("o", CultureInfo.InvariantCulture);
            File.WriteAllText(path, iso + "|" + TrialSign(iso));
        }
        catch { }
    }

    private static string TrialSign(string data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(TrialSecret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data)))[..16];
    }

    public static void SaveLicense(AppStoragePaths paths, string key)
    {
        var result = ValidateLicense(key);
        if (!result.IsValid)
            throw new ArgumentException(result.Message, nameof(key));

        paths.EnsureCreated();
        File.WriteAllText(GetLicensePath(paths), key.Trim());
    }

    public static bool IsAgentLimitRespected(LicensePayload? payload, int activeAgentCount)
        => payload is not null && activeAgentCount <= payload.MaxAgents;

    private static string NormalizeMachineId(string machineId)
        => NonHex.Replace(machineId ?? string.Empty, string.Empty).ToUpperInvariant();

    private static string NormalizeMac(string mac)
        => NonHex.Replace(mac ?? string.Empty, string.Empty).ToUpperInvariant();

    private static bool IsPreferredPhysicalType(NetworkInterfaceType type)
        => type == NetworkInterfaceType.Ethernet
        || type == NetworkInterfaceType.Wireless80211
        || type == NetworkInterfaceType.GigabitEthernet
        || type == NetworkInterfaceType.FastEthernetFx
        || type == NetworkInterfaceType.FastEthernetT;

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class LicensePayload
{
    [JsonPropertyName("v")]
    public int Version { get; set; }

    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = string.Empty;

    [JsonPropertyName("client")]
    public string Client { get; set; } = string.Empty;

    [JsonPropertyName("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("issuedAt")]
    public DateTimeOffset IssuedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("maxAgents")]
    public int MaxAgents { get; set; }

    [JsonPropertyName("features")]
    public string[] Features { get; set; } = [];
}

public record LicenseValidationResult(
    bool IsValid,
    string Message,
    string MachineId,
    LicensePayload? Payload = null)
{
    public static LicenseValidationResult Valid(string message, string machineId, LicensePayload payload)
        => new(true, message, machineId, payload);

    public static LicenseValidationResult Invalid(string message, string machineId, LicensePayload? payload = null)
        => new(false, message, machineId, payload);
}

public record LicenseStatus(
    bool IsValid,
    string Message,
    string MachineId,
    string? Key = null,
    string? PrimaryMac = null,
    LicensePayload? Payload = null)
{
    /// <summary>True si l'accès est autorisé via la période d'essai (pas une licence payante).</summary>
    public bool IsTrial { get; init; }
    /// <summary>Jours d'essai restants (0 si pas en essai ou essai terminé).</summary>
    public int TrialDaysRemaining { get; init; }
}
