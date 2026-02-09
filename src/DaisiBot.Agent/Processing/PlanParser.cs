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

    [GeneratedRegex(@"<plan>([\s\S]*?)</plan>", RegexOptions.Compiled)]
    private static partial Regex PlanTagRegex();

    [GeneratedRegex(@"<goal>([\s\S]*?)</goal>", RegexOptions.Compiled)]
    private static partial Regex GoalTagRegex();

    [GeneratedRegex(@"<step>([\s\S]*?)</step>", RegexOptions.Compiled)]
    private static partial Regex StepTagRegex();
}
