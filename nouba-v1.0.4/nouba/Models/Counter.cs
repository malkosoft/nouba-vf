using System.ComponentModel.DataAnnotations;

namespace Nouba.Models;

public class Counter
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public int ServiceTypeId { get; set; }
    public ServiceType? ServiceType { get; set; }
    public bool IsActive { get; set; } = true;
}
