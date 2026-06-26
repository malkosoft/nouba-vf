namespace Nouba.Infrastructure;

/// <summary>
/// Informations de version et de licence du produit Nouba.
/// Modifiez uniquement les champs marqués [CUSTOMIZABLE] pour chaque client.
/// </summary>
public static class LicenseInfo
{
    public const string ProductName    = "Nouba Pro";

    // v2.7.44 — AVANT : "Version" était une chaîne figée en dur ("2.7.26"),
    // totalement déconnectée de <Version> dans Nouba.csproj. Résultat : la
    // version affichée dans l'admin restait bloquée sur 2.7.26 pendant que
    // le produit avait déjà reçu près de 20 versions de corrections — source
    // de confusion (« le correctif n'a pas l'air d'être appliqué » alors
    // qu'il l'était). Désormais, on lit la version directement depuis les
    // métadonnées de l'assembly (générées par MSBuild à partir de <Version>
    // dans Nouba.csproj) : une seule source de vérité, plus jamais besoin de
    // mettre à jour ce fichier à la main à chaque changelog.
    public static readonly string Version = ResolveVersion();

    public const string Copyright      = "© 2026 Nouba. Tous droits réservés.";
    public const string Developer      = "Nouba Software";
    public const string Contact        = "support@nouba.dz";

    // [CUSTOMIZABLE] — À renseigner pour chaque installation client
    public static string ClientName    = string.Empty;   // ex: "Clinique El Amel"
    public static string ClientRef     = string.Empty;   // ex: "CLI-2026-001"
    public static string LicensedTo   = string.Empty;   // nom affiché dans l'admin
    public static DateTime LicenseDate = DateTime.MinValue;

    public static string FullVersion => $"{ProductName} v{Version}";

    public static string LicenseDisplay => string.IsNullOrWhiteSpace(ClientName)
        ? $"{ProductName} v{Version} — {Copyright}"
        : $"{ProductName} v{Version} — Licencié à : {ClientName} ({ClientRef}) — {Copyright}";

    private static string ResolveVersion()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            // <Version>2.7.43</Version> dans le .csproj devient automatiquement
            // l'AssemblyInformationalVersion à la compilation : c'est la forme
            // la plus proche de ce qu'on veut afficher (ex: "2.7.43", sans le
            // ".0" de fin qu'ajoute AssemblyVersion).
            var info = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // .NET peut suffixer l'InformationalVersion ("+<hash>" SourceLink,
                // ou un libellé) : on ne garde que le 1er jeton (ex. "1.0.0").
                var token = info.Split(new[] { ' ', '+' }, 2, StringSplitOptions.RemoveEmptyEntries);
                return token.Length > 0 ? token[0] : info;
            }

            var v = asm.GetName().Version;
            if (v != null) return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch { /* En cas d'échec improbable de lecture des métadonnées, on retombe ci-dessous. */ }
        return "2.7.26";
    }
}

