using QRCoder;
using System.Security.Cryptography;

namespace Nouba.Services;

/// <summary>
/// Génération de QR codes pour le suivi mobile des tickets.
/// Stratégie :
///  • PNG bytes pour l'écran (modale Confirmation borne, page /suivi).
///  • Bitmap matrix (booléens) pour l'impression ESC/POS thermique.
///  • Génération de tokens publics aléatoires (PublicId court).
/// Pas de dépendance à System.Drawing pour la matrice → fonctionne sur
/// Linux et Windows sans GDI+.
/// </summary>
public sealed class QrCodeService
{
    /// <summary>
    /// Génère un PublicId court alphanumérique (8 caractères, ~47 bits
    /// d'entropie). Suffisant pour qu'un client ne puisse pas deviner
    /// le ticket d'un autre, et assez court pour tenir dans une URL
    /// imprimée lisiblement.
    /// </summary>
    public static string NewPublicId(int length = 8)
    {
        // Alphabet sans caractères ambigus (0/O, 1/I, l).
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    /// <summary>
    /// PNG bytes pour affichage navigateur. Taille : pixelsPerModule contrôle
    /// la résolution (10 = ~250 px, 20 = ~500 px). On reste sobre.
    /// </summary>
    public byte[] GeneratePng(string content, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(content)) return Array.Empty<byte>();
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        // PngByteQRCode : pas de dépendance à System.Drawing.
        using var pngQr = new PngByteQRCode(qrData);
        return pngQr.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Matrice booléenne (true = pixel noir) pour impression ESC/POS.
    /// L'imprimante attend des données bitmap row-major, qu'on génère ailleurs.
    /// </summary>
    public bool[,] GenerateMatrix(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new bool[0, 0];
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var modules = qrData.ModuleMatrix;
        int n = modules.Count;
        var matrix = new bool[n, n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                matrix[y, x] = modules[y][x];
        return matrix;
    }
}
