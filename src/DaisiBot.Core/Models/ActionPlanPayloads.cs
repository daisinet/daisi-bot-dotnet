using System.Text.Json.Serialization;
using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class ActionPlanPayload
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<ActionPlanStepPayload> Steps { get; set; } = [];
}

public class ActionPlanStepPayload
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class ActionStepStartPayload
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }
}

public class ActionStepResultPayload
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("status")]
    public ActionItemStatus Status { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}
