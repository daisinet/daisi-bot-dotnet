using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class ActionPlan
{
    public string Goal { get; set; } = string.Empty;
    public List<ActionItem> Steps { get; set; } = [];
}

public class ActionItem
{
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Pending;
    public string? Result { get; set; }
    public string? Error { get; set; }
}
