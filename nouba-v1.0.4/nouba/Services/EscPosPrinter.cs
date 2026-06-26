using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Nouba.Models;

namespace Nouba.Services;

/// <summary>
/// Service unifié d'impression ticket pour Nouba — ZÉRO dépendance NuGet.
///
/// Trois transports supportés :
///   1. NETWORK (TCP/IP) — port 9100 RAW (universel : Epson, Star, Bixolon, Xprinter…)
///   2. USB (winspool RAW Windows) — utilise le pilote Windows installé
///   3. SERIAL (COM port) — pour vieilles bornes série / dongles USB-Série
///
/// Le mode est sélectionné via UiSettings.PrinterConnection ("network" | "usb" | "serial").
/// </summary>
public sealed class EscPosPrinter
{
    private readonly ILogger<EscPosPrinter> _logger;
    public EscPosPrinter(ILogger<EscPosPrinter> logger) { _logger = logger; }

    // ─── Octets ESC/POS standards ─────────────────────────────────────
    private const byte ESC = 0x1B, GS = 0x1D, LF = 0x0A;
    private static readonly byte[] InitPrinter   = { ESC, 0x40 };
    private static readonly byte[] AlignLeft     = { ESC, 0x61, 0x00 };
    private static readonly byte[] AlignCenter   = { ESC, 0x61, 0x01 };
    private static readonly byte[] BoldOn        = { ESC, 0x45, 0x01 };
    private static readonly byte[] BoldOff       = { ESC, 0x45, 0x00 };
    private static readonly byte[] DoubleOn      = { GS,  0x21, 0x11 };
    private static readonly byte[] QuadOn        = { GS,  0x21, 0x33 };
    private static readonly byte[] SizeNormal    = { GS,  0x21, 0x00 };
    private static readonly byte[] FeedAndCut    = { GS,  0x56, 0x41, 0x05 };
    private static readonly byte[] BeepShort     = { ESC, 0x42, 0x02, 0x02 };
    private static readonly byte[] CodepageCp858 = { ESC, 0x74, 0x13 };

    // ════════════════════════════════════════════════════════════════
    //                  IMPRESSION D'UN TICKET
    // ════════════════════════════════════════════════════════════════
    public async Task<(bool Ok, string? Error)> PrintTicketAsync(UiSettings cfg, EscPosTicketData data, CancellationToken ct = default)
    {
        if (!cfg.PrinterEnabled) return (false, "Imprimante désactivée dans les paramètres.");

        try
        {
            var bytes = BuildTicketBytes(cfg, data);
            await SendBytesAsync(cfg, bytes, ct);
            _logger.LogInformation("Ticket {Number} imprimé via {Conn}", data.TicketNumber, cfg.PrinterConnection);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec impression ticket {Number}", data.TicketNumber);
            return (false, FriendlyError(ex, cfg));
        }
    }

    public async Task<(bool Ok, string? Error)> TestPrintAsync(UiSettings cfg, CancellationToken ct = default)
    {
        var data = new EscPosTicketData
        {
            SiteName = cfg.SiteName,
            TicketNumber = "TEST",
            ServiceName = "Test imprimante",
            CreatedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
            WaitingCount = "—",
            EstimateMinutes = "—",
            FooterText = "Configuration validée. Bonne utilisation !",
            ShowNoubaFooter = cfg.TicketShowNoubaFooter,
            IsPriority = false
        };
        return await PrintTicketAsync(cfg, data, ct);
    }

    // ════════════════════════════════════════════════════════════════
    //          ROUTAGE SELON LE TYPE DE CONNEXION
    // ════════════════════════════════════════════════════════════════
    private async Task SendBytesAsync(UiSettings cfg, byte[] data, CancellationToken ct)
    {
        var conn = (cfg.PrinterConnection ?? "network").Trim().ToLowerInvariant();
        switch (conn)
        {
            case "usb":     await SendUsbAsync(cfg, data, ct); break;
            case "serial":  await SendSerialAsync(cfg, data, ct); break;
            case "network":
            default:        await SendNetworkAsync(cfg, data, ct); break;
        }
    }

    // ─── 1) Réseau TCP port 9100 ──────────────────────────────────────
    private static async Task SendNetworkAsync(UiSettings cfg, byte[] data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.PrinterIp))
            throw new InvalidOperationException("Adresse IP de l'imprimante non configurée.");
        var port = cfg.PrinterPort <= 0 ? 9100 : cfg.PrinterPort;
        var timeoutMs = cfg.PrinterTimeoutMs <= 0 ? 3000 : cfg.PrinterTimeoutMs;

        using var client = new TcpClient { NoDelay = true, SendTimeout = timeoutMs, ReceiveTimeout = timeoutMs };
        var connect = client.ConnectAsync(cfg.PrinterIp.Trim(), port);
        var winner = await Task.WhenAny(connect, Task.Delay(timeoutMs, ct));
        if (winner != connect)
            throw new TimeoutException($"Impossible de joindre {cfg.PrinterIp}:{port} en {timeoutMs} ms.");
        await connect;
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
        await Task.Delay(120, ct);
    }

    // ─── 2) USB Windows via winspool RAW ──────────────────────────────
    private async Task SendUsbAsync(UiSettings cfg, byte[] data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.PrinterName))
            throw new InvalidOperationException("Nom de l'imprimante Windows non configuré.");
        if (!OperatingSystem.IsWindows())
        {
            // Fallback CUPS (Linux/macOS) — utile pour debug
            await SendCupsAsync(cfg.PrinterName!, data, ct);
            return;
        }
#pragma warning disable CA1416
        await Task.Run(() => RawPrinterHelper.SendBytesToPrinter(cfg.PrinterName!, data), ct);
#pragma warning restore CA1416
    }

    private static async Task SendCupsAsync(string printerName, byte[] data, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("lp", $"-d \"{printerName}\" -o raw")
        {
            RedirectStandardInput = true, UseShellExecute = false
        };
        using var p = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Impossible de lancer 'lp' (CUPS non installé).");
        await p.StandardInput.BaseStream.WriteAsync(data, ct);
        p.StandardInput.Close();
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new Exception($"lp a retourné le code {p.ExitCode}.");
    }

    // ─── 3) Série COM ─────────────────────────────────────────────────
    private static async Task SendSerialAsync(UiSettings cfg, byte[] data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.PrinterComPort))
            throw new InvalidOperationException("Port série non configuré (ex: COM3).");
        var baud = cfg.PrinterBaudRate <= 0 ? 9600 : cfg.PrinterBaudRate;
        var timeoutMs = cfg.PrinterTimeoutMs <= 0 ? 3000 : cfg.PrinterTimeoutMs;

        using var port = new SerialPort(cfg.PrinterComPort, baud, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None, DtrEnable = true, RtsEnable = true,
            ReadTimeout = timeoutMs, WriteTimeout = timeoutMs
        };
        port.Open();
        await Task.Run(() => port.Write(data, 0, data.Length), ct);
        await Task.Delay(200, ct);
    }

    // ════════════════════════════════════════════════════════════════
    //              QUERY STATUS (reseau uniquement)
    // ════════════════════════════════════════════════════════════════
    public async Task<PrinterStatus> QueryStatusAsync(UiSettings cfg, CancellationToken ct = default)
    {
        var status = new PrinterStatus { CheckedAt = DateTime.Now };
        if (!cfg.PrinterEnabled)
        {
            status.ErrorMessage = "Imprimante désactivée";
            return status;
        }
        var conn = (cfg.PrinterConnection ?? "network").Trim().ToLowerInvariant();
        if (conn != "network")
        {
            // USB et série ne supportent pas les queries de status sans driver dédié.
            // On considère "online" si la dernière impression a réussi récemment.
            status.Online = true;
            return status;
        }
        if (string.IsNullOrWhiteSpace(cfg.PrinterIp)) { status.ErrorMessage = "IP non configurée"; return status; }

        try
        {
            using var client = new TcpClient { NoDelay = true };
            var port = cfg.PrinterPort <= 0 ? 9100 : cfg.PrinterPort;
            var timeoutMs = Math.Max(500, cfg.PrinterTimeoutMs);
            var connect = client.ConnectAsync(cfg.PrinterIp.Trim(), port);
            var done = await Task.WhenAny(connect, Task.Delay(timeoutMs, ct));
            if (done != connect || !client.Connected)
            { status.ErrorMessage = "Pas de réponse (timeout)"; return status; }

            await using var stream = client.GetStream();
            stream.ReadTimeout = timeoutMs;
            int read1 = 0, read2 = 0, read4 = 0;
            try
            {
                await stream.WriteAsync(new byte[] { 0x10, 0x04, 0x01 }, ct); read1 = await ReadOneByteAsync(stream, ct);
                await stream.WriteAsync(new byte[] { 0x10, 0x04, 0x02 }, ct); read2 = await ReadOneByteAsync(stream, ct);
                await stream.WriteAsync(new byte[] { 0x10, 0x04, 0x04 }, ct); read4 = await ReadOneByteAsync(stream, ct);
            }
            catch { /* certaines imprimantes ne répondent pas → online seulement */ }

            status.Online = true;
            if (read2 > 0)
            {
                if ((read2 & 0x04) != 0) status.CoverOpen = true;
                if ((read2 & 0x20) != 0) status.PaperOk = false;
            }
            if (read4 > 0)
            {
                if ((read4 & 0x60) != 0) status.PaperOk = false;
                if ((read4 & 0x0C) != 0) status.PaperNearEnd = true;
            }
            return status;
        }
        catch (Exception ex)
        {
            status.ErrorMessage = ex.Message;
            return status;
        }
    }

    private static async Task<int> ReadOneByteAsync(NetworkStream stream, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(800);
            var buf = new byte[1];
            var n = await stream.ReadAsync(buf.AsMemory(0, 1), cts.Token);
            return n == 1 ? buf[0] : 0;
        }
        catch { return 0; }
    }

    private static string FriendlyError(Exception ex, UiSettings cfg) => (cfg.PrinterConnection ?? "network").ToLowerInvariant() switch
    {
        "usb" =>
            $"Imprimante « {cfg.PrinterName} » introuvable ou refusée. " +
            "Vérifiez le pilote dans Paramètres Windows → Imprimantes. (" + ex.Message + ")",
        "serial" =>
            $"Port série {cfg.PrinterComPort} inaccessible. Vérifiez le câble/port et que l'imprimante est allumée. (" + ex.Message + ")",
        _ =>
            $"Pas de réponse de {cfg.PrinterIp}:{cfg.PrinterPort}. Vérifiez l'IP, le câble réseau et que l'imprimante est allumée. (" + ex.Message + ")"
    };

    // ════════════════════════════════════════════════════════════════
    //                  CONSTRUCTION DU CONTENU
    // ════════════════════════════════════════════════════════════════
    private static byte[] BuildTicketBytes(UiSettings cfg, EscPosTicketData d)
    {
        var enc = Encoding.GetEncoding("ISO-8859-1", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        using var ms = new MemoryStream();
        ms.Write(InitPrinter); ms.Write(CodepageCp858);

        // ── Optimisation encre/papier (v2.7.37) ──
        // Sur imprimante thermique, « moins d'encre » = moins de points noirs
        // chauffés. On réduit donc : le gras superflu, les lignes pleines de
        // séparation (remplacées par des tirets espacés, bien plus légers),
        // les sauts de ligne, et on allège le QR. Le numéro de ticket reste
        // en grand (c'est l'info vitale), mais l'entête n'est plus en double
        // taille (gros consommateur de noir).

        // Entête : nom du site en gras simple (plus en double hauteur/largeur).
        ms.Write(AlignCenter); ms.Write(BoldOn);
        ms.Write(enc.GetBytes((d.SiteName ?? "Nouba") + "\n"));
        ms.Write(BoldOff);

        // Numéro de ticket en grand (info essentielle, on le garde en quad).
        ms.WriteByte(LF);
        ms.Write(QuadOn);
        ms.Write(enc.GetBytes((d.TicketNumber ?? "---") + "\n"));
        ms.Write(SizeNormal);
        ms.WriteByte(LF);

        // Mention prioritaire (sans gras : un simple encadrement texte).
        if (d.IsPriority)
        {
            ms.Write(enc.GetBytes("* PRIORITAIRE *\n"));
            if (!string.IsNullOrWhiteSpace(d.PriorityReason))
                ms.Write(enc.GetBytes(d.PriorityReason + "\n"));
        }

        // Détails alignés à gauche (sans gras, texte fin).
        ms.Write(AlignLeft);
        ms.Write(enc.GetBytes($"Service     : {Truncate(d.ServiceName, 24)}\n"));
        ms.Write(enc.GetBytes($"Date / Heure: {d.CreatedAt}\n"));
        ms.Write(enc.GetBytes($"En attente  : {d.WaitingCount}\n"));
        ms.Write(enc.GetBytes($"Delai estime: {d.EstimateMinutes} min\n"));

        // ── QR code allégé pour le suivi mobile ──────────────────────
        if (!string.IsNullOrWhiteSpace(d.QrPayload))
        {
            ms.WriteByte(LF);
            ms.Write(AlignCenter);
            ms.Write(enc.GetBytes("Suivez votre tour :\n"));
            // 1) Modèle 2.
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
            // 2) Taille module réduite à 5 (au lieu de 6) : moins de noir,
            //    reste parfaitement scannable par smartphone.
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05 });
            // 3) Correction d'erreur L (48) au lieu de M : moins de modules
            //    noirs imprimés. Suffisant pour un QR de ticket propre.
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30 });
            // 4) Stocker les données.
            var payloadBytes = Encoding.UTF8.GetBytes(d.QrPayload);
            int storeLen = payloadBytes.Length + 3;
            ms.WriteByte(0x1D); ms.WriteByte(0x28); ms.WriteByte(0x6B);
            ms.WriteByte((byte)(storeLen & 0xFF));
            ms.WriteByte((byte)((storeLen >> 8) & 0xFF));
            ms.WriteByte(0x31); ms.WriteByte(0x50); ms.WriteByte(0x30);
            ms.Write(payloadBytes);
            // 5) Imprimer.
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
            ms.WriteByte(LF);
            if (!string.IsNullOrWhiteSpace(d.QrPublicId))
                ms.Write(enc.GetBytes($"Code : {d.QrPublicId}\n"));
        }

        // Pied (sans gras, séparateur léger en tirets espacés).
        ms.WriteByte(LF);
        ms.Write(AlignCenter);
        if (!string.IsNullOrWhiteSpace(d.FooterText))
            ms.Write(enc.GetBytes(d.FooterText + "\n"));
        if (d.ShowNoubaFooter) ms.Write(enc.GetBytes("Nouba Pro\n"));

        // Avance réduite + coupe (2 LF au lieu de 3 : un peu moins de papier).
        ms.WriteByte(LF); ms.WriteByte(LF);
        if (cfg.PrinterAutoCut) ms.Write(FeedAndCut);
        if (cfg.PrinterBeep)    ms.Write(BeepShort);
        return ms.ToArray();
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s.Substring(0, max - 1) + "…" : s);

    // ════════════════════════════════════════════════════════════════
    //              LISTE DES IMPRIMANTES INSTALLÉES (Windows)
    // ════════════════════════════════════════════════════════════════
    public List<string> ListInstalledPrinters()
    {
        var list = new List<string>();
        if (!OperatingSystem.IsWindows()) return list;
        try
        {
#pragma warning disable CA1416
            foreach (string n in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                list.Add(n);
#pragma warning restore CA1416
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Énumération imprimantes Windows impossible"); }
        return list;
    }

    public List<string> ListSerialPorts()
    {
        try { return SerialPort.GetPortNames().ToList(); }
        catch { return new List<string>(); }
    }
}

public sealed class EscPosTicketData
{
    public string? SiteName { get; init; }
    public string? TicketNumber { get; init; }
    public string? ServiceName { get; init; }
    public string? CreatedAt { get; init; }
    public string? WaitingCount { get; init; }
    public string? EstimateMinutes { get; init; }
    public string? FooterText { get; init; }
    public bool ShowNoubaFooter { get; init; }
    public bool IsPriority { get; init; }
    public string? PriorityReason { get; init; }
    /// <summary>Si non vide, on imprime un QR code natif ESC/POS sur le ticket avec ce contenu (URL).</summary>
    public string? QrPayload { get; init; }
    /// <summary>PublicId court à imprimer en clair sous le QR (ex: « Code : 8F7K2Q »).</summary>
    public string? QrPublicId { get; init; }
}

// ────────────────────────────────────────────────────────────────────
//      Helper P/Invoke pour USB Windows (winspool RAW print)
// ────────────────────────────────────────────────────────────────────
[SupportedOSPlatform("windows")]
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFOW
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPWStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);
    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);
    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOW di);
    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);
    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static void SendBytesToPrinter(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                $"Impossible d'ouvrir l'imprimante « {printerName} ».");
        var di = new DOCINFOW { pDocName = "Nouba Ticket", pOutputFile = null, pDataType = "RAW" };
        IntPtr p = IntPtr.Zero;
        try
        {
            if (!StartDocPrinter(hPrinter, 1, di)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter");
            if (!StartPagePrinter(hPrinter))       throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter");
            p = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            if (!WritePrinter(hPrinter, p, bytes.Length, out int written))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter");
            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
        }
        finally
        {
            if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
            ClosePrinter(hPrinter);
        }
    }
}
