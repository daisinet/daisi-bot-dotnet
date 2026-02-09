using DaisiBot.Core.Models;
using DaisiBot.Core.Models.Skills;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data;

public class DaisiBotDbContext(DbContextOptions<DaisiBotDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<AuthState> AuthStates => Set<AuthState>();
    public DbSet<InstalledSkill> InstalledSkills => Set<InstalledSkill>();

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
        ];

        foreach (var (name, sql) in migrations)
        {
            if (existingColumns.Contains(name)) continue;
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
