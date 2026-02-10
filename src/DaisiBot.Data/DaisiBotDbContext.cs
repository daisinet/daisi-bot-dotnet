using System.Text.Json;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Models;
using DaisiBot.Core.Models.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DaisiBot.Data;

public class DaisiBotDbContext(DbContextOptions<DaisiBotDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<AuthState> AuthStates => Set<AuthState>();
    public DbSet<InstalledSkill> InstalledSkills => Set<InstalledSkill>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<BotInstance> Bots => Set<BotInstance>();
    public DbSet<BotLogEntry> BotLogEntries => Set<BotLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasMany(c => c.Messages)
             .WithOne()
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.ConversationId);
        });

        modelBuilder.Entity<UserSettings>(e =>
        {
            e.HasKey(s => s.Id);
        });

        modelBuilder.Entity<AuthState>(e =>
        {
            e.HasKey(a => a.Id);
            e.Ignore(a => a.IsAuthenticated);
        });

        modelBuilder.Entity<InstalledSkill>(e =>
        {
            e.HasKey(i => new { i.SkillId, i.AccountId });
        });

        modelBuilder.Entity<Skill>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.RequiredToolGroups).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ToolGroupSelection>>(v, (JsonSerializerOptions?)null) ?? new List<ToolGroupSelection>());
            e.Property(s => s.Tags).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        });

        modelBuilder.Entity<BotInstance>(e =>
        {
            e.HasKey(b => b.Id);
        });

        modelBuilder.Entity<BotLogEntry>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.BotId);
        });
    }

    public static string GetDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DaisiBot");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "daisibot.db");
    }

    /// <summary>
    /// Adds any columns missing from an existing database.
    /// EnsureCreated only works on a fresh DB; this handles schema drift.
    /// </summary>
    public async Task ApplyMigrationsAsync()
    {
        var conn = Database.GetDbConnection();
        await conn.OpenAsync();

        // Get existing columns in the Settings table
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Settings)";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                existingColumns.Add(reader.GetString(1));
        }

        // Columns that may be missing from older databases
        (string Name, string Sql)[] migrations =
        [
            ("EnabledSkillIdsCsv", "ALTER TABLE Settings ADD COLUMN EnabledSkillIdsCsv TEXT NOT NULL DEFAULT ''"),
            ("HostModeEnabled", "ALTER TABLE Settings ADD COLUMN HostModeEnabled INTEGER NOT NULL DEFAULT 0"),
            ("ModelFolderPath", "ALTER TABLE Settings ADD COLUMN ModelFolderPath TEXT NOT NULL DEFAULT ''"),
            ("LlamaRuntime", "ALTER TABLE Settings ADD COLUMN LlamaRuntime INTEGER NOT NULL DEFAULT 0"),
            ("ContextSize", "ALTER TABLE Settings ADD COLUMN ContextSize INTEGER NOT NULL DEFAULT 2048"),
            ("GpuLayerCount", "ALTER TABLE Settings ADD COLUMN GpuLayerCount INTEGER NOT NULL DEFAULT -1"),
            ("BatchSize", "ALTER TABLE Settings ADD COLUMN BatchSize INTEGER NOT NULL DEFAULT 512"),
            ("NetworkHostEnabled", "ALTER TABLE Settings ADD COLUMN NetworkHostEnabled INTEGER NOT NULL DEFAULT 0"),
            ("LastScreen", "ALTER TABLE Settings ADD COLUMN LastScreen TEXT NOT NULL DEFAULT 'bots'"),
        ];

        foreach (var (name, sql) in migrations)
        {
            if (existingColumns.Contains(name)) continue;
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Ensure Skills table exists for older databases
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Skills (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    ShortDescription TEXT NOT NULL DEFAULT '',
                    Author TEXT NOT NULL DEFAULT '',
                    AccountId TEXT NOT NULL DEFAULT '',
                    Version TEXT NOT NULL DEFAULT '1.0.0',
                    IconUrl TEXT NOT NULL DEFAULT '',
                    RequiredToolGroups TEXT NOT NULL DEFAULT '[]',
                    Tags TEXT NOT NULL DEFAULT '[]',
                    Visibility INTEGER NOT NULL DEFAULT 0,
                    Status INTEGER NOT NULL DEFAULT 0,
                    ReviewedBy TEXT,
                    ReviewedAt TEXT,
                    RejectionReason TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    DownloadCount INTEGER NOT NULL DEFAULT 0,
                    SystemPromptTemplate TEXT NOT NULL DEFAULT ''
                )
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Ensure Bots table exists
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Bots (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Label TEXT NOT NULL DEFAULT 'New Bot',
                    Goal TEXT NOT NULL DEFAULT '',
                    Persona TEXT,
                    Status INTEGER NOT NULL DEFAULT 0,
                    LastError TEXT,
                    RetryGuidance TEXT,
                    ScheduleType INTEGER NOT NULL DEFAULT 0,
                    ScheduleIntervalMinutes INTEGER NOT NULL DEFAULT 0,
                    NextRunAt TEXT,
                    ModelName TEXT NOT NULL DEFAULT '',
                    Temperature REAL NOT NULL DEFAULT 0.7,
                    MaxTokens INTEGER NOT NULL DEFAULT 32000,
                    EnabledSkillIdsCsv TEXT NOT NULL DEFAULT '',
                    PendingQuestion TEXT,
                    ExecutionCount INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    LastRunAt TEXT
                )
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Ensure BotLogEntries table exists
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS BotLogEntries (
                    Id TEXT NOT NULL PRIMARY KEY,
                    BotId TEXT NOT NULL,
                    ExecutionNumber INTEGER NOT NULL DEFAULT 0,
                    Level INTEGER NOT NULL DEFAULT 0,
                    Message TEXT NOT NULL DEFAULT '',
                    Detail TEXT,
                    Timestamp TEXT NOT NULL
                )
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Index on BotLogEntries.BotId
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_BotLogEntries_BotId ON BotLogEntries (BotId)";
            await cmd.ExecuteNonQueryAsync();
        }

        // Add RetryGuidance column to Bots if missing
        var botColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Bots)";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                botColumns.Add(reader.GetString(1));
        }

        if (!botColumns.Contains("RetryGuidance"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE Bots ADD COLUMN RetryGuidance TEXT";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
