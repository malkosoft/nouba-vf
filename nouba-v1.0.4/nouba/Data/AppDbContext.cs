using Microsoft.EntityFrameworkCore;
using Nouba.Models;

namespace Nouba.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ServiceType> Services => Set<ServiceType>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<CallHistory> CallHistories => Set<CallHistory>();
    public DbSet<UiSettings> UiSettings => Set<UiSettings>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Indexes : énorme gain sur les requêtes File d'attente ─────────
        // Composite ServiceTypeId + Status : utilisé par Agent, Borne, Display.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.ServiceTypeId, t.Status })
            .HasDatabaseName("IX_Tickets_Service_Status");

        // CreatedAt : statistiques journalières + numérotation.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_Tickets_CreatedAt");

        // Garantie production : aucun doublon de numéro dans un même service et un même jour.
        // C'est la base SQLite qui protège, même si deux bornes cliquent simultanément.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.ServiceTypeId, t.TicketDay, t.Number })
            .IsUnique()
            .HasDatabaseName("UX_Tickets_Service_Day_Number");

        // CalledAt : derniers tickets appelés (Agent / Display).
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.CalledAt)
            .HasDatabaseName("IX_Tickets_CalledAt");

        // CallHistory : Display tire les 30 derniers en boucle (toutes les 3s).
        modelBuilder.Entity<CallHistory>()
            .HasIndex(c => c.CalledAt)
            .HasDatabaseName("IX_CallHistory_CalledAt");

        modelBuilder.Entity<CallHistory>()
            .HasIndex(c => c.AgentId)
            .HasDatabaseName("IX_CallHistory_AgentId");

        // Login : recherche rapide + intégrité.
        modelBuilder.Entity<Agent>()
            .HasIndex(a => a.Login)
            .IsUnique()
            .HasDatabaseName("IX_Agents_Login");

        modelBuilder.Entity<AdminUser>()
            .HasIndex(a => a.Username)
            .IsUnique()
            .HasDatabaseName("IX_AdminUsers_Username");

        // ── Données par défaut (inchangées) ───────────────────────────────
        modelBuilder.Entity<ServiceType>().HasData(
            new ServiceType { Id = 1, Name = "Accueil général", Code = "A", IsActive = true, DisplayOrder = 1, ButtonColor = "#1d4ed8", TextColor = "#ffffff", LogoUrl = null },
            new ServiceType { Id = 2, Name = "Consultation", Code = "C", IsActive = true, DisplayOrder = 2, ButtonColor = "#0f766e", TextColor = "#ffffff", LogoUrl = null },
            new ServiceType { Id = 3, Name = "Paiement", Code = "P", IsActive = true, DisplayOrder = 3, ButtonColor = "#b45309", TextColor = "#ffffff", LogoUrl = null }
        );

        modelBuilder.Entity<Counter>().HasData(
            new Counter { Id = 1, Name = "Guichet 1", ServiceTypeId = 1, IsActive = true },
            new Counter { Id = 2, Name = "Guichet 2", ServiceTypeId = 2, IsActive = true },
            new Counter { Id = 3, Name = "Guichet 3", ServiceTypeId = 3, IsActive = true }
        );

        modelBuilder.Entity<Agent>().HasData(
            new Agent { Id = 1, FullName = "Agent 1", Login = "agent1", Password = string.Empty, PasswordHash = string.Empty, PasswordSalt = string.Empty, CounterId = 1, ServiceTypeId = 1, IsAdmin = false, IsActive = false },
            new Agent { Id = 2, FullName = "Agent 2", Login = "agent2", Password = string.Empty, PasswordHash = string.Empty, PasswordSalt = string.Empty, CounterId = 2, ServiceTypeId = 2, IsAdmin = false, IsActive = false }
        );

        modelBuilder.Entity<UiSettings>().HasData(
            new UiSettings
            {
                Id = 1,
                SiteName = "Nouba",
                BorneTitleFr = "Choisissez votre service",
                BorneTitleAr = "اختر الخدمة",
                BorneTitleTz = "Fren tameẓluḍt-ik",
                BorneTitleEn = "Choose your service",
                BorneSubtitleFr = "Touchez un bouton pour imprimer un ticket.",
                BorneSubtitleAr = "المس الزر لطباعة تذكرتك.",
                BorneSubtitleTz = "Ssed ɣef tqeffalt akken ad tsiggeḍ tikect.",
                BorneSubtitleEn = "Tap a button to print a ticket.",
                TicketsClosed = false,
                TicketClosureMessageFr = "La prise de ticket est temporairement fermée. Merci de revenir plus tard.",
                TicketClosureMessageAr = "تم إغلاق أخذ التذاكر مؤقتًا. يرجى العودة لاحقًا.",
                TicketClosureMessageTz = "Tukksa n tikacin tettwaqfel akka tura. Uɣal-d ticki.",
                TicketClosureMessageEn = "Ticketing is temporarily closed. Please come back later.",
                DisplayTitleFr = "Numéro appelé",
                DisplayTitleAr = "الرقم المُنادى",
                DisplayTitleTz = "Uṭṭun yettwasiwlen",
                DisplayTitleEn = "Now serving",
                DisplayBannerTextFr = "Bienvenue - Merci de suivre les indications affichées",
                DisplayBannerTextAr = "مرحبا - يرجى متابعة التعليمات المعروضة",
                DisplayBannerTextTz = "Ansuf - Ma ulac aɣilif ḍfer tilmawin yettbanen",
                DisplayBannerTextEn = "Welcome - Please follow the displayed instructions",
                HeaderTextFr = "Nouba - Gestion de file d'attente",
                HeaderTextAr = "نوبة - إدارة طابور الانتظار",
                HeaderTextTz = "Nouba - Asefrak n udras n ugani",
                HeaderTextEn = "Nouba - Queue management",
                HeaderImageUrl = null,
                DisplayBannerImageUrl = null,
                DisplayBackgroundImageUrl = null,
                DisplayBackgroundVideoUrl = null,
                DisplayLogoUrl = null,
                DisplayAccentColor = "#0f766e",
                DisplayCardColor = "#0f172a",
                DisplayHistoryColor = "#111827",
                DisplayTextColor = "#ffffff",
                DisplayTicketBgColor = "#f0e2a3",
                DisplayFooterColor = "#c21f4a",
                ShowClockOnDisplay = true,
                EnableAnimations = true,
                EnableFullScreenButton = true,
                EnableVoiceAnnouncements = true,
                ShowServiceNameOnDisplay = true,
                ShowWaitingCountOnAgent = true,
                EnableBorneAutoReturn = true,
                EnablePriorityTickets = true,
                BorneAutoReturnSeconds = 8,
                ThemePreset = "hopital",
                DefaultLanguage = "fr",
                VoiceGender = "female",
                VoiceRepeatCount = 2,
                VoiceRate = 0.90,
                VoicePitch = 1.0,
                VoiceVolume = 1.0,
                DisplayHistoryRows = 5,
                ShowCounterNameOnDisplay = true,
                ShowCallTimeOnDisplay = true,
                DisplayLayout = "standard",
                ServerUrl = "http://0.0.0.0:5000"
            }
        );

        // TicketStatus stored as integer (default EF behaviour) — faster queries and consistent with seed data.
    }
}
