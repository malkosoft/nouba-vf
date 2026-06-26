namespace Nouba.Models;

public class CallHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string CounterName { get; set; } = string.Empty;
    public DateTime CalledAt { get; set; } = DateTime.Now;
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
}
