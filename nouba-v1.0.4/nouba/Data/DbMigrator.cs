using Microsoft.EntityFrameworkCore;

namespace Nouba.Data;

/// <summary>
/// Applique les migrations manuelles sur une DB SQLite existante.
/// Détecte automatiquement les vrais noms de tables et ajoute les colonnes manquantes.
/// </summary>
public static class DbMigrator
{
    public static void Apply(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        var allTables = GetAllTables(connection);
        using var cmd = connection.CreateCommand();

        var uiTable = allTables.FirstOrDefault(t => t.Equals("UiSettings", StringComparison.OrdinalIgnoreCase));
        if (uiTable != null)
        {
            var cols = GetColumns(connection, uiTable);
            var newColumns = new (string Name, string Type, string Default)[]
            {
                ("DisplayTextColor", "TEXT", "'#ffffff'"),
                ("DisplayTicketBgColor", "TEXT", "'#f0e2a3'"),
                ("DisplayFooterColor", "TEXT", "'#c21f4a'"),
                ("EnableBorneAutoReturn", "INTEGER", "1"),
                ("EnablePriorityTickets", "INTEGER", "1"),
                ("BorneAutoReturnSeconds", "INTEGER", "8"),
                ("ShowServiceNameOnDisplay", "INTEGER", "1"),
                ("ShowWaitingCountOnAgent", "INTEGER", "1"),
                ("ShowCounterNameOnDisplay", "INTEGER", "1"),
                ("ShowCallTimeOnDisplay", "INTEGER", "1"),
                ("DisplayHistoryRows", "INTEGER", "5"),
                ("DisplayLayout", "TEXT", "'standard'"),
                ("VoiceRepeatCount", "INTEGER", "2"),
                ("VoiceRate", "REAL", "0.9"),
                ("VoicePitch", "REAL", "1.0"),
                ("VoiceVolume", "REAL", "1.0"),
                ("HeaderTextFr", "TEXT", "''"),
                ("HeaderTextAr", "TEXT", "''"),
                ("HeaderTextTz", "TEXT", "''"),
                ("HeaderTextEn", "TEXT", "''"),
                ("HeaderImageUrl", "TEXT", "NULL"),
                ("DisplayLogoUrl", "TEXT", "NULL"),
                ("ServerUrl", "TEXT", "'http://0.0.0.0:5000'"),
                // ── Impression ESC/POS (v2.2) ─────────────────────────────
                ("PrintMode", "TEXT", "'all'"),
                ("PrinterEnabled", "INTEGER", "1"),
                ("PrinterIp", "TEXT", "'192.168.1.100'"),
                ("PrinterPort", "INTEGER", "9100"),
                ("PrinterName", "TEXT", "NULL"),
                ("PrinterConnection", "TEXT", "'network'"),
                ("PrinterComPort", "TEXT", "NULL"),
                ("PrinterBaudRate", "INTEGER", "9600"),
                ("TicketWidth", "INTEGER", "80"),
                ("PrinterTimeoutMs", "INTEGER", "3000"),
                ("PrinterAutoCut", "INTEGER", "1"),
                ("PrinterBeep", "INTEGER", "0"),
                ("TicketShowLogo", "INTEGER", "1"),
                ("TicketShowQr", "INTEGER", "0"),
                ("TicketShowNoubaFooter", "INTEGER", "1"),
                ("QrFollowEnabled", "INTEGER", "1"),
                ("QrFollowPublicUrl", "TEXT", "NULL"),
                ("QrFollowSiteId", "TEXT", "NULL"),
                ("QrFollowApiKey", "TEXT", "NULL"),
                ("QrFollowShowOnDisplay", "INTEGER", "0"),
                ("TicketFooterFr", "TEXT", "'Merci de votre visite'"),
                ("TicketFooterAr", "TEXT", "'شكرا لزيارتكم'"),
                ("TicketFooterTz", "TEXT", "'Tanemmirt ɣef tirza-nwen'"),
                ("TicketFooterEn", "TEXT", "'Thank you for your visit'"),
                ("ReportShowClientLogo", "INTEGER", "1"),
                ("ReportShowNoubaLogo", "INTEGER", "1"),
                ("PrinterMonitoringEnabled", "INTEGER", "1"),
                ("PrinterMonitorIntervalSec", "INTEGER", "30"),
                ("PrinterAlertSound", "INTEGER", "1"),
                // ── TTS Piper (v2.6) ──
                ("TtsPiperEnabled", "INTEGER", "0"),
                ("TtsPiperBinaryPath", "TEXT", "NULL"),
                ("TtsModelFr", "TEXT", "'fr_FR-siwis-medium.onnx'"),
                ("TtsModelEn", "TEXT", "'en_US-amy-medium.onnx'"),
                ("TtsModelAr", "TEXT", "'ar_JO-kareem-low.onnx'"),
                ("TtsLengthScale", "REAL", "1.0"),
                ("TtsTimeoutMs", "INTEGER", "5000"),
                // ── Effet wow (#1, #3) ──
                ("WowDurationMs", "INTEGER", "2200"),
                ("WowSoundChoice", "TEXT", "'chime'"),
                // ── Borne : sélecteur de langue (#14) ──
                ("BorneShowLanguageSwitcher", "INTEGER", "1"),
                ("BorneEnabledLanguages", "TEXT", "'fr,ar,tz,en'"),
            };
            foreach (var (name, type, defaultVal) in newColumns)
                if (!cols.Contains(name)) ExecAlter(cmd, uiTable, name, type, defaultVal);
        }

        var svcTable = allTables.FirstOrDefault(t => t.Equals("ServiceType", StringComparison.OrdinalIgnoreCase) || t.Equals("Services", StringComparison.OrdinalIgnoreCase));
        if (svcTable != null)
        {
            var cols = GetColumns(connection, svcTable);
            if (!cols.Contains("LogoUrl")) ExecAlter(cmd, svcTable, "LogoUrl", "TEXT", "NULL");
            if (!cols.Contains("Emoji")) ExecAlter(cmd, svcTable, "Emoji", "TEXT", "NULL");
            if (!cols.Contains("ButtonColor")) ExecAlter(cmd, svcTable, "ButtonColor", "TEXT", "'#0d6efd'");
            if (!cols.Contains("TextColor")) ExecAlter(cmd, svcTable, "TextColor", "TEXT", "'#ffffff'");
            if (!cols.Contains("DisplayOrder")) ExecAlter(cmd, svcTable, "DisplayOrder", "INTEGER", "0");
            // ── Noms multilingues (#12) ──
            if (!cols.Contains("NameAr")) ExecAlter(cmd, svcTable, "NameAr", "TEXT", "NULL");
            if (!cols.Contains("NameTz")) ExecAlter(cmd, svcTable, "NameTz", "TEXT", "NULL");
            if (!cols.Contains("NameEn")) ExecAlter(cmd, svcTable, "NameEn", "TEXT", "NULL");
        }

        var counterTable = allTables.FirstOrDefault(t => t.Equals("Counter", StringComparison.OrdinalIgnoreCase) || t.Equals("Counters", StringComparison.OrdinalIgnoreCase));
        if (counterTable != null)
        {
            var cols = GetColumns(connection, counterTable);
            if (!cols.Contains("IsActive")) ExecAlter(cmd, counterTable, "IsActive", "INTEGER", "1");
        }

        // ── Rôles administrateurs (v2.7.55) ──────────────────────────────
        // Ajout du rôle client/fournisseur. Sur une base EXISTANTE (colonne
        // absente), tous les administrateurs déjà présents sont promus
        // "fournisseur" pour ne perdre aucun accès. Sur une base neuve, la
        // colonne est créée par EnsureCreated avec le défaut "client", donc
        // ce bloc est ignoré et les comptes créés ensuite restent "client".
        var adminTable = allTables.FirstOrDefault(t => t.Equals("AdminUsers", StringComparison.OrdinalIgnoreCase) || t.Equals("AdminUser", StringComparison.OrdinalIgnoreCase));
        if (adminTable != null)
        {
            var cols = GetColumns(connection, adminTable);
            if (!cols.Contains("Role"))
            {
                ExecAlter(cmd, adminTable, "Role", "TEXT", "'client'");
                try
                {
                    cmd.CommandText = $"UPDATE \"{adminTable}\" SET \"Role\" = 'fournisseur' WHERE \"Role\" IS NULL OR trim(\"Role\") = '' OR \"Role\" = 'client';";
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }
        }

        var agentTable = allTables.FirstOrDefault(t => t.Equals("Agent", StringComparison.OrdinalIgnoreCase) || t.Equals("Agents", StringComparison.OrdinalIgnoreCase));
        if (agentTable != null)
        {
            var cols = GetColumns(connection, agentTable);
            if (!cols.Contains("IsActive")) ExecAlter(cmd, agentTable, "IsActive", "INTEGER", "1");
            if (!cols.Contains("PasswordHash")) ExecAlter(cmd, agentTable, "PasswordHash", "TEXT", "''");
            if (!cols.Contains("PasswordSalt")) ExecAlter(cmd, agentTable, "PasswordSalt", "TEXT", "''");
        }

        var ticketTable = allTables.FirstOrDefault(t => t.Equals("Ticket", StringComparison.OrdinalIgnoreCase) || t.Equals("Tickets", StringComparison.OrdinalIgnoreCase));
        if (ticketTable != null)
        {
            var cols = GetColumns(connection, ticketTable);
            if (!cols.Contains("IsPriority")) ExecAlter(cmd, ticketTable, "IsPriority", "INTEGER", "0");
            if (!cols.Contains("PriorityReason")) ExecAlter(cmd, ticketTable, "PriorityReason", "TEXT", "NULL");
            if (!cols.Contains("TicketDay")) ExecAlter(cmd, ticketTable, "TicketDay", "TEXT", "''");
            if (!cols.Contains("PublicId")) ExecAlter(cmd, ticketTable, "PublicId", "TEXT", "NULL");

            try
            {
                cmd.CommandText = $@"UPDATE ""{ticketTable}""
SET ""TicketDay"" = CASE
    WHEN ""TicketDay"" IS NOT NULL AND length(trim(""TicketDay"")) = 10 THEN ""TicketDay""
    WHEN ""CreatedAt"" IS NOT NULL AND length(CAST(""CreatedAt"" AS TEXT)) >= 10 THEN substr(CAST(""CreatedAt"" AS TEXT), 1, 10)
    ELSE date('now')
END
WHERE ""TicketDay"" IS NULL OR length(trim(""TicketDay"")) <> 10;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            RepairDuplicateTicketNumbers(cmd, ticketTable);
        }

        var historyTable = allTables.FirstOrDefault(t => t.Equals("CallHistories", StringComparison.OrdinalIgnoreCase) || t.Equals("CallHistory", StringComparison.OrdinalIgnoreCase));
        if (historyTable != null)
        {
            var cols = GetColumns(connection, historyTable);
            if (!cols.Contains("AgentId")) ExecAlter(cmd, historyTable, "AgentId", "INTEGER", "NULL");
            if (!cols.Contains("AgentName")) ExecAlter(cmd, historyTable, "AgentName", "TEXT", "NULL");
        }

        if (ticketTable != null)
        {
            ExecCreateIndex(cmd, "IX_Tickets_Service_Status", ticketTable, "ServiceTypeId, Status");
            ExecCreateIndex(cmd, "IX_Tickets_CreatedAt", ticketTable, "CreatedAt");
            ExecCreateIndex(cmd, "IX_Tickets_CalledAt", ticketTable, "CalledAt");
            ExecCreateUniqueIndex(cmd, "UX_Tickets_Service_Day_Number", ticketTable, "ServiceTypeId, TicketDay, Number");
        }
        if (historyTable != null)
        {
            ExecCreateIndex(cmd, "IX_CallHistory_CalledAt", historyTable, "CalledAt");
            ExecCreateIndex(cmd, "IX_CallHistory_AgentId", historyTable, "AgentId");
        }

        try { cmd.CommandText = "ANALYZE;"; cmd.ExecuteNonQuery(); } catch { }
    }

    private static void ExecCreateUniqueIndex(System.Data.Common.DbCommand cmd, string indexName, string table, string columns)
    {
        // Do NOT swallow exceptions here: if the unique index cannot be created
        // (e.g., duplicates remain after repair), the anti-duplicate protection is
        // silently absent. Let it propagate so the operator can diagnose the issue.
        cmd.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS \"{indexName}\" ON \"{table}\" ({columns});";
        cmd.ExecuteNonQuery();
    }

    private static void RepairDuplicateTicketNumbers(System.Data.Common.DbCommand cmd, string table)
    {
        try
        {
            cmd.CommandText = $@"WITH ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY ServiceTypeId, TicketDay, Number ORDER BY Id) AS rn
    FROM ""{table}""
)
UPDATE ""{table}""
SET Number = Number || '-D' || Id
WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static void ExecCreateIndex(System.Data.Common.DbCommand cmd, string indexName, string table, string columns)
    {
        try
        {
            cmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{table}\" ({columns});";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static void ExecAlter(System.Data.Common.DbCommand cmd, string table, string column, string type, string defaultVal)
    {
        try
        {
            cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type} DEFAULT {defaultVal};";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static HashSet<string> GetAllTables(System.Data.Common.DbConnection connection)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) tables.Add(reader.GetString(0));
        return tables;
    }

    private static HashSet<string> GetColumns(System.Data.Common.DbConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) columns.Add(reader.GetString(1));
        return columns;
    }
}
