using System.Text.RegularExpressions;
using DaisiBot.Core.Models;

namespace DaisiBot.Agent.Processing;

public static partial class PlanParser
{
    private const int MaxSteps = 5;

    public static ActionPlan? Parse(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        // Try numbered list first (preferred for small models)
        var listPlan = ParseNumberedList(rawOutput);
        if (listPlan is not null)
            return listPlan;

        // Fallback to XML (backward compat for larger models)
        return ParseXml(rawOutput);
    }

    /// <summary>
    /// Parse numbered list format:
    /// Goal: One sentence
    /// 1. First step
    /// 2. Second step
    /// </summary>
    public static ActionPlan? ParseNumberedList(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        var lines = rawOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // Try to extract goal from "Goal: ..." line
        string? goal = null;
        foreach (var line in lines)
        {
            var goalMatch = GoalLineRegex().Match(line);
            if (goalMatch.Success)
            {
                goal = goalMatch.Groups[1].Value.Trim();
                break;
            }
        }

        var plan = new ActionPlan { Goal = goal ?? string.Empty };

        foreach (var line in lines)
        {
            if (plan.Steps.Count >= MaxSteps) break;

            var match = NumberedListRegex().Match(line);
            if (match.Success)
            {
                var desc = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    plan.Steps.Add(new ActionItem
                    {
                        StepNumber = plan.Steps.Count + 1,
                        Description = desc
                    });
                }
                continue;
            }

            var bulletMatch = BulletListRegex().Match(line);
            if (bulletMatch.Success)
            {
                var desc = bulletMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    plan.Steps.Add(new ActionItem
                    {
                        StepNumber = plan.Steps.Count + 1,
                        Description = desc
                    });
                }
            }
        }

        return plan.Steps.Count > 0 ? plan : null;
    }

    /// <summary>
    /// Parse XML format (backward compat):
    /// &lt;plan&gt;&lt;goal&gt;...&lt;/goal&gt;&lt;step&gt;...&lt;/step&gt;&lt;/plan&gt;
    /// </summary>
    public static ActionPlan? ParseXml(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        var planMatch = PlanTagRegex().Match(rawOutput);
        if (!planMatch.Success)
            return null;

        var planContent = planMatch.Groups[1].Value;

        var goalMatch = GoalTagRegex().Match(planContent);
        if (!goalMatch.Success)
            return null;

        var goal = goalMatch.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(goal))
            return null;

        var stepMatches = StepTagRegex().Matches(planContent);
        if (stepMatches.Count == 0)
            return null;

        var plan = new ActionPlan { Goal = goal };

        var stepCount = Math.Min(stepMatches.Count, MaxSteps);
        for (var i = 0; i < stepCount; i++)
        {
            var description = stepMatches[i].Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(description))
            {
                plan.Steps.Add(new ActionItem
                {
                    StepNumber = plan.Steps.Count + 1,
                    Description = description
                });
            }
        }

        return plan.Steps.Count > 0 ? plan : null;
    }

    /// <summary>
    /// Fallback parser for when the model doesn't use XML tags.
    /// Handles numbered lists like "1. Do this\n2. Do that" and bullet lists like "- Do this".
    /// </summary>
    public static ActionPlan? ParseFallback(string rawOutput, string goal)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        var plan = new ActionPlan { Goal = goal };

        var lines = rawOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (plan.Steps.Count >= MaxSteps) break;

            var match = NumberedListRegex().Match(line);
            if (match.Success)
            {
                var desc = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    plan.Steps.Add(new ActionItem
                    {
                        StepNumber = plan.Steps.Count + 1,
                        Description = desc
                    });
                }
                continue;
            }

            var bulletMatch = BulletListRegex().Match(line);
            if (bulletMatch.Success)
            {
                var desc = bulletMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    plan.Steps.Add(new ActionItem
                    {
                        StepNumber = plan.Steps.Count + 1,
                        Description = desc
                    });
                }
            }
        }

        return plan.Steps.Count > 0 ? plan : null;
    }

    [GeneratedRegex(@"<plan>([\s\S]*?)</plan>", RegexOptions.Compiled)]
    private static partial Regex PlanTagRegex();

    [GeneratedRegex(@"<goal>([\s\S]*?)</goal>", RegexOptions.Compiled)]
    private static partial Regex GoalTagRegex();

    [GeneratedRegex(@"<step>([\s\S]*?)</step>", RegexOptions.Compiled)]
    private static partial Regex StepTagRegex();

    [GeneratedRegex(@"^\d+[\.\)]\s*(.+)$", RegexOptions.Compiled)]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"^[-\*\u2022]\s*(.+)$", RegexOptions.Compiled)]
    private static partial Regex BulletListRegex();

    [GeneratedRegex(@"^Goal:\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GoalLineRegex();
}
