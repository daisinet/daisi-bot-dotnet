using Daisi.Orc.Core.Data.Db;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Web.Services;

public class CosmoSkillService(Cosmo cosmo) : ISkillService
{
    public async Task<List<Skill>> GetPublicSkillsAsync(string? searchQuery = null, string? tag = null)
    {
        var cosmoSkills = await cosmo.GetPublicApprovedSkillsAsync(searchQuery, tag);
        return cosmoSkills.Select(MapFromCosmo).ToList();
    }

    public async Task<Skill?> GetSkillAsync(string id)
    {
        var cosmoSkill = await cosmo.GetSkillByIdAsync(id);
        return cosmoSkill is not null ? MapFromCosmo(cosmoSkill) : null;
    }

    public async Task<List<Skill>> GetMySkillsAsync(string accountId)
    {
        var cosmoSkills = await cosmo.GetSkillsByAccountAsync(accountId);
        return cosmoSkills.Select(MapFromCosmo).ToList();
    }

    public async Task<List<InstalledSkill>> GetInstalledSkillsAsync(string accountId)
    {
        var installed = await cosmo.GetInstalledSkillsAsync(accountId);
        return installed.Select(i => new InstalledSkill
        {
            SkillId = i.SkillId,
            AccountId = i.AccountId,
            EnabledAt = i.EnabledAt
        }).ToList();
    }

    public async Task InstallSkillAsync(string accountId, string skillId)
    {
        await cosmo.InstallSkillAsync(accountId, skillId);
    }

    public async Task UninstallSkillAsync(string accountId, string skillId)
    {
        await cosmo.UninstallSkillAsync(accountId, skillId);
    }

    public async Task<Skill> CreateSkillAsync(Skill skill)
    {
        var cosmoSkill = MapToCosmo(skill);
        var created = await cosmo.CreateSkillAsync(cosmoSkill);
        return MapFromCosmo(created);
    }

    public async Task UpdateSkillAsync(Skill skill)
    {
        var cosmoSkill = MapToCosmo(skill);
        await cosmo.UpdateSkillAsync(cosmoSkill);
    }

    public async Task SubmitForReviewAsync(string skillId)
    {
        var skill = await cosmo.GetSkillByIdAsync(skillId);
        if (skill is null) throw new InvalidOperationException("Skill not found.");
        skill.Status = "PendingReview";
        await cosmo.UpdateSkillAsync(skill);
    }

    public async Task<List<Skill>> GetPendingReviewsAsync()
    {
        var skills = await cosmo.GetPendingReviewSkillsAsync();
        return skills.Select(MapFromCosmo).ToList();
    }

    public async Task ReviewSkillAsync(string skillId, string reviewerEmail, bool approved, string? comment = null)
    {
        var skill = await cosmo.GetSkillByIdAsync(skillId);
        if (skill is null) throw new InvalidOperationException("Skill not found.");

        var status = approved ? "Approved" : "Rejected";

        skill.Status = status;
        skill.ReviewedBy = reviewerEmail;
        skill.ReviewedAt = DateTime.UtcNow;
        if (!approved && !string.IsNullOrWhiteSpace(comment))
            skill.RejectionReason = comment;

        await cosmo.UpdateSkillAsync(skill);

        await cosmo.CreateSkillReviewAsync(new Daisi.Orc.Core.Data.Models.Skills.SkillReview
        {
            SkillId = skillId,
            ReviewerEmail = reviewerEmail,
            Status = status,
            Comment = comment ?? string.Empty
        });
    }

    private static Skill MapFromCosmo(Daisi.Orc.Core.Data.Models.Skills.Skill s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        ShortDescription = s.ShortDescription,
        Author = s.Author,
        AccountId = s.AccountId,
        Version = s.Version,
        IconUrl = s.IconUrl,
        RequiredToolGroups = s.RequiredToolGroups
            .Select(g => Enum.TryParse<ToolGroupSelection>(g, out var v) ? v : ToolGroupSelection.InformationTools)
            .ToList(),
        Tags = s.Tags,
        Visibility = Enum.TryParse<SkillVisibility>(s.Visibility, out var vis) ? vis : SkillVisibility.Private,
        Status = Enum.TryParse<SkillStatus>(s.Status, out var st) ? st : SkillStatus.Draft,
        ReviewedBy = s.ReviewedBy,
        ReviewedAt = s.ReviewedAt,
        RejectionReason = s.RejectionReason,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        DownloadCount = s.DownloadCount,
        SystemPromptTemplate = s.SystemPromptTemplate
    };

    private static Daisi.Orc.Core.Data.Models.Skills.Skill MapToCosmo(Skill s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        ShortDescription = s.ShortDescription,
        Author = s.Author,
        AccountId = s.AccountId,
        Version = s.Version,
        IconUrl = s.IconUrl,
        RequiredToolGroups = s.RequiredToolGroups.Select(g => g.ToString()).ToList(),
        Tags = s.Tags,
        Visibility = s.Visibility.ToString(),
        Status = s.Status.ToString(),
        ReviewedBy = s.ReviewedBy,
        ReviewedAt = s.ReviewedAt,
        RejectionReason = s.RejectionReason,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        DownloadCount = s.DownloadCount,
        SystemPromptTemplate = s.SystemPromptTemplate
    };
}
