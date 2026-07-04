using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class UiSettings
{
    public int Id { get; set; }

    [StringLength(120)]
    public string SiteName { get; set; } = "Nouba";

    [StringLength(250)] public string BorneTitleFr { get; set; } = "Choisissez votre service";
    [StringLength(250)] public string BorneTitleAr { get; set; } = "اختر الخدمة";
    [StringLength(250)] public string BorneTitleTz { get; set; } = "Fren tameẓluḍt-ik";
    [StringLength(250)] public string BorneTitleEn { get; set; } = "Choose your service";

    [StringLength(250)] public string BorneSubtitleFr { get; set; } = "Touchez un bouton pour imprimer un ticket.";
    [StringLength(250)] public string BorneSubtitleAr { get; set; } = "المس الزر لطباعة تذكرتك.";
    [StringLength(250)] public string BorneSubtitleTz { get; set; } = "Ssed ɣef tqeffalt akken ad tsiggeḍ tikect.";
    [StringLength(250)] public string BorneSubtitleEn { get; set; } = "Tap a button to print your ticket.";

    public bool TicketsClosed { get; set; }
    [StringLength(350)] public string TicketClosureMessageFr { get; set; } = "La prise de ticket est temporairement fermée. Merci de revenir plus tard.";
    [StringLength(350)] public string TicketClosureMessageAr { get; set; } = "تم إغلاق أخذ التذاكر مؤقتًا. يرجى العودة لاحقًا.";
    [StringLength(350)] public string TicketClosureMessageTz { get; set; } = "Tukksa n tikacin tettwaqfel akka tura. Uɣal-d ticki.";
    [StringLength(350)] public string TicketClosureMessageEn { get; set; } = "Ticketing is temporarily closed. Please come back later.";

    [StringLength(250)] public string DisplayTitleFr { get; set; } = "Numéro appelé";
    [StringLength(250)] public string DisplayTitleAr { get; set; } = "الرقم المُنادى";
    [StringLength(250)] public string DisplayTitleTz { get; set; } = "Uṭṭun yettwasiwlen";
    [StringLength(250)] public string DisplayTitleEn { get; set; } = "Now serving";

    [StringLength(350)] public string? DisplayBannerTextFr { get; set; } = "Bienvenue - Merci de suivre les indications affichées";
    [StringLength(350)] public string? DisplayBannerTextAr { get; set; } = "مرحبا - يرجى متابعة التعليمات المعروضة";
    [StringLength(350)] public string? DisplayBannerTextTz { get; set; } = "Ansuf - Ma ulac aɣilif ḍfer tilmawin yettbanen";
    [StringLength(350)] public string? DisplayBannerTextEn { get; set; } = "Welcome - Please follow the displayed instructions";

    [StringLength(250)] public string? HeaderTextFr { get; set; } = "Bienvenue dans votre espace d'accueil";
    [StringLength(250)] public string? HeaderTextAr { get; set; } = "مرحبًا بكم في فضاء الاستقبال";
    [StringLength(250)] public string? HeaderTextTz { get; set; } = "Ansuf ɣer usiweḍ-inek";
    [StringLength(250)] public string? HeaderTextEn { get; set; } = "Welcome to the service area";

    [StringLength(500)] public string? HeaderImageUrl { get; set; }
    [StringLength(500)] public string? DisplayBannerImageUrl { get; set; }
    [StringLength(500)] public string? DisplayBackgroundImageUrl { get; set; }
    [StringLength(500)] public string? DisplayBackgroundVideoUrl { get; set; }
    [StringLength(500)] public string? DisplayLogoUrl { get; set; }

    [StringLength(7)] public string DisplayAccentColor { get; set; } = "#2563eb";
    [StringLength(7)] public string DisplayCardColor { get; set; } = "#0f172a";
    [StringLength(7)] public string DisplayHistoryColor { get; set; } = "#111827";
    [StringLength(7)] public string DisplayTextColor { get; set; } = "#ffffff";
    [StringLength(7)] public string DisplayTicketBgColor { get; set; } = "#f0e2a3";
    [StringLength(7)] public string DisplayFooterColor { get; set; } = "#c21f4a";

    public bool ShowClockOnDisplay { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool EnableFullScreenButton { get; set; } = true;
    public bool EnableVoiceAnnouncements { get; set; } = true;
    public bool ShowServiceNameOnDisplay { get; set; } = true;
    public bool ShowWaitingCountOnAgent { get; set; } = true;
    public bool EnableBorneAutoReturn { get; set; } = true;
    public bool EnablePriorityTickets { get; set; } = true;
    public int BorneAutoReturnSeconds { get; set; } = 8;

    [StringLength(20)] public string ThemePreset { get; set; } = "clinique";
    [StringLength(10)] public string DefaultLanguage { get; set; } = "fr";
    [StringLength(10)] public string VoiceGender { get; set; } = "female";
    public int VoiceRepeatCount { get; set; } = 1;
    public double VoiceRate { get; set; } = 0.90;
    public double VoicePitch { get; set; } = 1.0;
    public double VoiceVolume { get; set; } = 1.0;

    public int DisplayHistoryRows { get; set; } = 5;
    public bool ShowCounterNameOnDisplay { get; set; } = true;
    public bool ShowCallTimeOnDisplay { get; set; } = true;
    [StringLength(20)] public string DisplayLayout { get; set; } = "standard";

    // ── Réseau ────────────────────────────────────────────────────
    // URL d'écoute du serveur. Modifiable depuis l'admin sans toucher à appsettings.json.
    // http://0.0.0.0:5000 = accès réseau LAN | http://127.0.0.1:5000 = local uniquement
    [StringLength(200)] public string ServerUrl { get; set; } = "http://0.0.0.0:5000";

    // ── Imprimante ────────────────────────────────────────────────
    /// <summary>
    /// Mode d'impression principal :
    ///   "escpos"       → Imprimante thermique réseau ESC/POS (TCP, port 9100 par défaut).
    ///   "chrome-kiosk" → Impression silencieuse via Chrome --kiosk-printing.
    ///   "browser"      → Boîte de dialogue navigateur classique (CTRL+P).
    ///   "all"          → ESC/POS d'abord + fallback navigateur (recommandé pour fiabilité maximale).
    /// </summary>
    [StringLength(20)] public string PrintMode { get; set; } = "all";

    /// <summary>Active la prise en charge ESC/POS (envoi direct TCP).</summary>
    public bool PrinterEnabled { get; set; } = true;

    /// <summary>
    /// Type de connexion ESC/POS :
    ///   "network" → TCP/IP port 9100 (recommandé, universel)
    ///   "usb"     → Pilote Windows installé (winspool RAW)
    ///   "serial"  → Port COM (vieilles bornes)
    /// </summary>
    [StringLength(20)] public string PrinterConnection { get; set; } = "network";

    /// <summary>Adresse IP ou nom DNS de l'imprimante thermique réseau.</summary>
    [StringLength(120)] public string PrinterIp { get; set; } = "192.168.1.100";

    /// <summary>Port TCP de l'imprimante (Epson/Star/Bixolon : 9100 par défaut).</summary>
    public int PrinterPort { get; set; } = 9100;

    /// <summary>Nom du modèle ou identifiant lisible pour l'admin (informatif).</summary>
    [StringLength(120)] public string? PrinterName { get; set; }

    /// <summary>Port série COM si PrinterConnection = "serial" (ex: COM3, /dev/ttyUSB0).</summary>
    [StringLength(40)] public string? PrinterComPort { get; set; }

    /// <summary>Vitesse en bauds du port série (mode "serial"). Standard : 9600 ou 19200.</summary>
    public int PrinterBaudRate { get; set; } = 9600;

    /// <summary>Largeur du papier en mm : 58 (mini) ou 80 (standard).</summary>
    public int TicketWidth { get; set; } = 80;

    /// <summary>Délai d'attente réseau (ms) pour la connexion à l'imprimante.</summary>
    public int PrinterTimeoutMs { get; set; } = 3000;

    /// <summary>Couper le papier automatiquement après chaque ticket (commande ESC/POS GS V).</summary>
    public bool PrinterAutoCut { get; set; } = true;

    /// <summary>Faire bipper l'imprimante après chaque ticket (commande ESC B).</summary>
    public bool PrinterBeep { get; set; } = false;

    /// <summary>Imprimer le logo (en monochrome) sur le ticket.</summary>
    public bool TicketShowLogo { get; set; } = true;

    /// <summary>Imprimer un QR code (numéro de ticket) sur le ticket.</summary>
    public bool TicketShowQr { get; set; } = false;

    /// <summary>Afficher logo Nouba en pied de ticket.</summary>
    public bool TicketShowNoubaFooter { get; set; } = true;

    // ─── Suivi mobile par QR code (v2.7.12+) ──────────────────────
    /// <summary>Active la fonctionnalité QR de suivi mobile (borne + ticket imprimé + page /suivi).</summary>
    public bool QrFollowEnabled { get; set; } = true;

    /// <summary>
    /// URL publique racine utilisée pour le QR code. Si vide, on utilise l'URL
    /// locale (Wi-Fi). Ex : "https://suivi.nouba.app" → QR pointera vers
    /// https://suivi.nouba.app/suivi/{publicId}.
    /// Si vide, fallback sur l'URL où Nouba tourne (LAN local).
    /// </summary>
    [StringLength(250)] public string? QrFollowPublicUrl { get; set; }

    /// <summary>Identifiant établissement pour la sync cloud (option future).</summary>
    [StringLength(64)] public string? QrFollowSiteId { get; set; }

    /// <summary>Clé API établissement pour la sync cloud (option future).</summary>
    [StringLength(120)] public string? QrFollowApiKey { get; set; }

    /// <summary>Afficher un QR général sur l'écran TV pointant vers /suivi (sans publicId).</summary>
    public bool QrFollowShowOnDisplay { get; set; } = false;

    /// <summary>
    /// Sécurité : adresses IP (ou plages CIDR) autorisées à joindre l'administration
    /// (/Admin, /Printer, /Ai) DEPUIS le réseau local, EN PLUS du poste hôte (localhost,
    /// toujours autorisé). Séparateurs : virgule, point-virgule, espace ou retour ligne.
    /// Exemples : « 192.168.1.50 » ou « 192.168.1.0/24 ». Vide = localhost uniquement.
    /// </summary>
    [StringLength(500)] public string? AdminAllowedIps { get; set; }

    [StringLength(250)] public string? TicketFooterFr { get; set; } = "Merci de votre visite";
    [StringLength(250)] public string? TicketFooterAr { get; set; } = "شكرا لزيارتكم";
    [StringLength(250)] public string? TicketFooterTz { get; set; } = "Tanemmirt ɣef tirza-nwen";
    [StringLength(250)] public string? TicketFooterEn { get; set; } = "Thank you for your visit";

    // ── Rapport PDF ───────────────────────────────────────────────
    /// <summary>Inclure le logo client (HeaderImageUrl) en haut du rapport PDF.</summary>
    public bool ReportShowClientLogo { get; set; } = true;

    /// <summary>Inclure le logo Nouba en haut du rapport PDF.</summary>
    public bool ReportShowNoubaLogo { get; set; } = true;

    // ── Monitoring imprimante (v2.4) ──────────────────────────────
    /// <summary>Activer le monitoring périodique de l'imprimante (ping + status ESC/POS).</summary>
    public bool PrinterMonitoringEnabled { get; set; } = true;

    /// <summary>Intervalle entre chaque vérification (secondes).</summary>
    public int PrinterMonitorIntervalSec { get; set; } = 30;

    /// <summary>Jouer un son d'alerte dans l'admin quand l'imprimante est en défaut.</summary>
    public bool PrinterAlertSound { get; set; } = true;

    // ════════════════════════════════════════════════════════════════
    //   TTS « VOIX RÉALISTE » — Piper (offline) avec fallback navigateur
    //   ─────────────────────────────────────────────────────────────────
    //   ⚠️ STABILITÉ v1.0.0 : les champs Tts* ci-dessous sont HÉRITÉS et
    //   INERTES. Le service PiperTtsService ne les lit pas : il détecte seul
    //   les modèles dans wwwroot/tts/piper/ et fixe le débit dans
    //   ResolveLengthScale (AR femme 1.15 / AR homme 1.0 / FR-EN 1.0). Ils ne
    //   sont pas non plus exposés dans l'admin (aucun risque côté client).
    //   Pour régler la voix : page /tts-tuning.html (vitesse + tashkeel à
    //   l'oreille), puis report des valeurs retenues dans ResolveLengthScale.
    //   Conservés tels quels pour éviter une migration SQLite non testable ici.
    // ════════════════════════════════════════════════════════════════
    /// <summary>Active Piper TTS (binaire externe). Si false ou indisponible, on utilise Web Speech API du navigateur.</summary>
    public bool TtsPiperEnabled { get; set; } = false;

    /// <summary>Chemin du binaire piper.exe. Vide = recherche dans wwwroot/tts/piper/piper.exe (par défaut).</summary>
    [StringLength(300)] public string? TtsPiperBinaryPath { get; set; }

    /// <summary>Modèle français (relatif à wwwroot/tts/models/ ou chemin absolu). Ex: fr_FR-siwis-medium.onnx</summary>
    [StringLength(200)] public string? TtsModelFr { get; set; } = "fr_FR-siwis-medium.onnx";

    /// <summary>Modèle anglais. Ex: en_US-amy-medium.onnx</summary>
    [StringLength(200)] public string? TtsModelEn { get; set; } = "en_US-amy-medium.onnx";

    /// <summary>Modèle arabe (qualité moyenne, arabe standard). Ex: ar_JO-kareem-low.onnx</summary>
    [StringLength(200)] public string? TtsModelAr { get; set; } = "ar_JO-kareem-low.onnx";

    /// <summary>Vitesse de parole. 1.0 = normal, &gt;1 = plus lent, &lt;1 = plus rapide. Recommandé : 1.0–1.2 pour annonces.</summary>
    public double TtsLengthScale { get; set; } = 1.0;

    /// <summary>Timeout maximum (ms) pour la synthèse d'une phrase. Au-delà → fallback navigateur.</summary>
    public int TtsTimeoutMs { get; set; } = 5000;

    // ── Effet wow (#1, #3) ────────────────────────────────────────
    /// <summary>Durée en ms de l'animation plein-écran lors d'un appel. 0 = désactivée.</summary>
    public int WowDurationMs { get; set; } = 2200;

    /// <summary>
    /// Son joué juste avant l'annonce vocale.
    ///   "chime"  → bi-bong doux (défaut, Web Audio)
    ///   "ding"   → note unique courte
    ///   "gong"   → résonance longue
    ///   "none"   → aucun son (annonce vocale directe)
    /// </summary>
    [StringLength(20)] public string WowSoundChoice { get; set; } = "chime";

    // ── Borne : sélecteur de langue (#14) ────────────────────────
    /// <summary>Affiche le sélecteur de langue sur la borne client.</summary>
    public bool BorneShowLanguageSwitcher { get; set; } = true;

    /// <summary>Langues disponibles sur la borne, séparées par des virgules. Ex : "fr,ar"</summary>
    [StringLength(40)] public string BorneEnabledLanguages { get; set; } = "fr,ar,tz,en";
}
