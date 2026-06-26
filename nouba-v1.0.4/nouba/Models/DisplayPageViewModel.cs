namespace Nouba.Models;

public class DisplayPageViewModel
{
    public UiSettings Settings { get; set; } = new();
    public string CurrentLanguage { get; set; } = "fr";
    public string DisplayTitle { get; set; } = "Numéro appelé";
    public string WaitingText { get; set; } = "En attente";
    public string CounterText { get; set; } = "Guichet";
    public string ServiceText { get; set; } = "Service";
    public string RecentText { get; set; } = "Historique récent";
    public string BannerText { get; set; } = string.Empty;
    public string HeaderText { get; set; } = string.Empty;
    public string Announcement { get; set; } = string.Empty;
    public Dictionary<string, string> ServiceLogos { get; set; } = new();
    public List<CallHistory> History { get; set; } = new();
    public List<DisplayActiveCallViewModel> ActiveCalls { get; set; } = new();
}
