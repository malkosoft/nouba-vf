using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class AdminUser
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string PasswordSalt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Rôle de l'administrateur :
    ///   - "client"      : gère le métier (services, guichets, agents, textes,
    ///                     affichage, marque). N'accède PAS aux réglages techniques.
    ///   - "fournisseur" : accès total, y compris imprimante, réseau, licence,
    ///                     diagnostics et gestion des comptes administrateurs.
    /// Par défaut "client". Le premier compte créé à l'installation est
    /// "fournisseur", et les installations existantes sont migrées en
    /// "fournisseur" pour ne perdre aucun accès.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "client";
}
