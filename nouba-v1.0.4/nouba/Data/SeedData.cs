using Nouba.Helpers;
using Nouba.Models;

namespace Nouba.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();

        if (!context.UiSettings.Any())
        {
            context.UiSettings.Add(new UiSettings());
        }

        // Sécurité production : ne crée plus de compte admin par défaut.
        // Au premier lancement, /Admin/Login affiche l'écran de création du premier admin.


        foreach (var agent in context.Agents.Where(a => string.IsNullOrWhiteSpace(a.PasswordHash) && !string.IsNullOrWhiteSpace(a.Password)))
        {
            var agentHash = PasswordHasher.HashPassword(agent.Password);
            agent.PasswordHash = agentHash.Hash;
            agent.PasswordSalt = agentHash.Salt;
            agent.Password = string.Empty;
        }

        // Sécurité production : les anciennes bases pouvaient contenir deux
        // agents de démonstration actifs avec le mot de passe faible "1234".
        // On désactive uniquement ces comptes s'ils sont encore inchangés.
        var legacyDefaultAgents = context.Agents.Where(a =>
            (a.Login == "agent1" && a.PasswordHash == "N9tcvIlvIJ05wWliPR6gwqHE+xEmX+zIQQvHmSRjmBU=" && a.PasswordSalt == "ARnEfcu4qitPJDmSF1UGTA==")
            || (a.Login == "agent2" && a.PasswordHash == "JSfkMQecvNAC+Ih+xqMs2TacB4+JX4zU40++wwR+j9w=" && a.PasswordSalt == "wYK54xhBST/cJ/TqnTlVYA=="));
        foreach (var agent in legacyDefaultAgents)
        {
            agent.IsActive = false;
            agent.Password = string.Empty;
            agent.PasswordHash = string.Empty;
            agent.PasswordSalt = string.Empty;
        }

        context.SaveChanges();
    }
}
