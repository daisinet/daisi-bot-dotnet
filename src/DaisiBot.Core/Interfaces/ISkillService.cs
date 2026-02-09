using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Core.Interfaces;

public interface ISkillService
{
    Task<List<Skill>> GetPublicSkillsAsync(string? searchQuery = null, string? tag = null);
    Task<Skill?> GetSkillAsync(string id);
    Task<List<Skill>> GetMySkillsAsync(string accountId);
    Task<List<InstalledSkill>> GetInstalledSkillsAsync(string accountId);
    Task InstallSkillAsync(string accountId, string skillId);
    Task UninstallSkillAsync(string accountId, string skillId);
    Task<Skill> CreateSkillAsync(Skill skill);
    Task UpdateSkillAsync(Skill skill);
    Task SubmitForReviewAsync(string skillId);
    Task<List<Skill>> GetPendingReviewsAsync();
    Task ReviewSkillAsync(string skillId, string reviewerEmail, bool approved, string? comment = null);
}
