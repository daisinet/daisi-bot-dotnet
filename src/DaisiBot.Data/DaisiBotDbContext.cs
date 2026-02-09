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
}
