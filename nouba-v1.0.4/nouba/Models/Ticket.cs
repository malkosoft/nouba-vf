using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class Ticket
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string Number { get; set; } = string.Empty;

    public int ServiceTypeId { get; set; }
    public ServiceType? ServiceType { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Waiting;

    public bool IsPriority { get; set; }

    [StringLength(120)]
    public string? PriorityReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Jour métier du ticket au format yyyy-MM-dd.
    // Sert à garantir un numéro unique par service et par jour, même en cas de clics simultanés.
    [StringLength(10)]
    public string TicketDay { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public DateTime? CalledAt { get; set; }
    public int? CounterId { get; set; }
    public Counter? Counter { get; set; }

    /// <summary>
    /// Identifiant public court (8 caractères alphanumériques) utilisé dans
    /// l'URL de suivi mobile : /suivi/{PublicId}. Difficile à deviner pour
    /// éviter qu'un client puisse accéder à un autre ticket que le sien.
    /// Généré à la création du ticket dans BorneController.CreateTicket.
    /// </summary>
    [StringLength(16)]
    public string? PublicId { get; set; }
}
