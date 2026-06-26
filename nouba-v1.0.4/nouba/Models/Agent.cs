using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class Agent
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Login { get; set; } = string.Empty;

    // Ancien champ conservé uniquement pour migration des bases existantes.
    // Ne plus l'utiliser pour authentifier un agent.
    [StringLength(50)]
    public string Password { get; set; } = string.Empty;

    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(256)]
    public string PasswordSalt { get; set; } = string.Empty;

    public int CounterId { get; set; }
    public Counter? Counter { get; set; }

    public int ServiceTypeId { get; set; }
    public ServiceType? ServiceType { get; set; }

    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}
