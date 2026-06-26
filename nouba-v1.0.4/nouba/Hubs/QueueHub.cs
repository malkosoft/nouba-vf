using Microsoft.AspNetCore.SignalR;

namespace Nouba.Hubs;

public class QueueHub : Hub
{
    /// <summary>
    /// Les rafraîchissements globaux sont émis uniquement côté serveur via IHubContext.
    /// Cette méthode reste présente pour compatibilité client, mais ne diffuse plus à tous
    /// afin d'éviter qu'un visiteur anonyme puisse spammer l'écran d'affichage.
    /// </summary>
    public Task BroadcastRefresh() => Task.CompletedTask;
}
