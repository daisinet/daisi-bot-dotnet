using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Core.Interfaces;

public interface ISkillFileLoader
{
    Task<List<Skill>> LoadAllAsync(CancellationToken ct = default);
    Task<List<Skill>> LoadFromCategoryAsync(string category, CancellationToken ct = default);
}
