using System.Text;
using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Agent.Skills;

public static class SkillPromptBuilder
{
    public static string BuildSystemPrompt(string basePrompt, IEnumerable<Skill> enabledSkills)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(basePrompt))
        {
            sb.AppendLine(basePrompt);
        }

        var skills = enabledSkills.ToList();
        if (skills.Count == 0)
            return sb.ToString().TrimEnd();

        sb.AppendLine();
        sb.AppendLine("--- Active Skills ---");

        foreach (var skill in skills)
        {
            if (!string.IsNullOrWhiteSpace(skill.SystemPromptTemplate))
            {
                sb.AppendLine();
                sb.AppendLine($"[Skill: {skill.Name} v{skill.Version}]");
                sb.AppendLine(skill.SystemPromptTemplate);
            }
        }

        return sb.ToString().TrimEnd();
    }
}
