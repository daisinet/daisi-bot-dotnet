using DaisiBot.Core.Models.Skills;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteInstalledSkillStore(IDbContextFactory<DaisiBotDbContext> dbFactory)
{
    public async Task<List<InstalledSkill>> GetAllAsync(string accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.InstalledSkills
            .Where(i => i.AccountId == accountId)
            .OrderByDescending(i => i.EnabledAt)
            .ToListAsync();
    }

    public async Task AddAsync(InstalledSkill skill)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.InstalledSkills.FindAsync(skill.SkillId, skill.AccountId);
        if (existing is null)
        {
            db.InstalledSkills.Add(skill);
            await db.SaveChangesAsync();
        }
    }

    public async Task RemoveAsync(string skillId, string accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.InstalledSkills.FindAsync(skillId, accountId);
        if (existing is not null)
        {
            db.InstalledSkills.Remove(existing);
            await db.SaveChangesAsync();
        }
    }
}
