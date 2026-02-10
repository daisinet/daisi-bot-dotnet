using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models.Skills;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteSkillService(IDbContextFactory<DaisiBotDbContext> dbFactory) : ISkillService
{
    public async Task<List<Skill>> GetPublicSkillsAsync(string? searchQuery = null, string? tag = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Skills
            .Where(s => s.Visibility == SkillVisibility.Public && s.Status == SkillStatus.Approved);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var term = searchQuery.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                s.Description.ToLower().Contains(term));
        }

        return await query.OrderByDescending(s => s.DownloadCount).ToListAsync();
    }

    public async Task<Skill?> GetSkillAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Skills.FindAsync(id);
    }

    public async Task<List<Skill>> GetMySkillsAsync(string accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Skills
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<InstalledSkill>> GetInstalledSkillsAsync(string accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.InstalledSkills
            .Where(i => i.AccountId == accountId)
            .OrderByDescending(i => i.EnabledAt)
            .ToListAsync();
    }

    public async Task InstallSkillAsync(string accountId, string skillId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.InstalledSkills.FindAsync(skillId, accountId);
        if (existing is null)
        {
            db.InstalledSkills.Add(new InstalledSkill
            {
                SkillId = skillId,
                AccountId = accountId
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task UninstallSkillAsync(string accountId, string skillId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.InstalledSkills.FindAsync(skillId, accountId);
        if (existing is not null)
        {
            db.InstalledSkills.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Skill> CreateSkillAsync(Skill skill)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (string.IsNullOrEmpty(skill.Id))
            skill.Id = Guid.NewGuid().ToString();
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return skill;
    }

    public async Task UpdateSkillAsync(Skill skill)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        skill.UpdatedAt = DateTime.UtcNow;
        db.Skills.Update(skill);
        await db.SaveChangesAsync();
    }

    public Task SubmitForReviewAsync(string skillId) => Task.CompletedTask;

    public Task<List<Skill>> GetPendingReviewsAsync() => Task.FromResult(new List<Skill>());

    public Task ReviewSkillAsync(string skillId, string reviewerEmail, bool approved, string? comment = null)
        => Task.CompletedTask;
}
