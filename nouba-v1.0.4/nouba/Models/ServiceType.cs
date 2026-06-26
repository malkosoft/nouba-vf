using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class ServiceType
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(5)]
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    [StringLength(7)]
    public string ButtonColor { get; set; } = "#0d6efd";

    [StringLength(7)]
    public string TextColor { get; set; } = "#ffffff";

    [StringLength(255)]
    public string? LogoUrl { get; set; }

    /// <summary>Emoji premium optionnel affiché sur la tuile de la borne
    /// (ex. 🏥, 💳, 📄). Vide = pas d'emoji.</summary>
    [StringLength(16)]
    public string? Emoji { get; set; }

    // ── Noms multilingues (#12) ───────────────────────────────────
    /// <summary>Nom en arabe. Vide = fallback sur Name.</summary>
    [StringLength(120)] public string? NameAr { get; set; }

    /// <summary>Nom en Tamazight. Vide = fallback sur Name.</summary>
    [StringLength(120)] public string? NameTz { get; set; }

    /// <summary>Nom en anglais. Vide = fallback sur Name.</summary>
    [StringLength(120)] public string? NameEn { get; set; }
}
