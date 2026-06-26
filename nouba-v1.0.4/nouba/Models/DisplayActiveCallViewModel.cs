namespace Nouba.Models;

public class DisplayActiveCallViewModel
{
    public string TicketNumber { get; set; } = string.Empty;
    public string CounterName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime CalledAt { get; set; }
}
