namespace Nouba.Models;

public class AgentDashboardStat
{
    public Agent Agent { get; set; } = new();
    public int Calls { get; set; }
    public int Served { get; set; }
    public int WaitingService { get; set; }
    public DateTime? LastCall { get; set; }
}
