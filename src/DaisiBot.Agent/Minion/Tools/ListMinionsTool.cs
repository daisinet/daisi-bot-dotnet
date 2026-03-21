using System.Text;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class ListMinionsTool : DaisiToolBase
{
    private readonly MinionProcessManager _processManager;

    public ListMinionsTool(MinionProcessManager processManager)
    {
        _processManager = processManager;
    }

    public override string Id => "summoner-list-minions";
    public override string Name => "List Minions";

    public override string UseInstructions =>
        "Lists all spawned minions and their current status. " +
        "Keywords: list, show, minions, workers, status.";

    public override ToolParameter[] Parameters => [];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        return new ToolExecutionContext
        {
            ExecutionMessage = "Listing minions",
            ExecutionTask = Task.Run(() =>
            {
                var sb = new StringBuilder();
                var minions = _processManager.Minions;

                if (minions.Count == 0)
                {
                    sb.AppendLine("No minions spawned yet.");
                }
                else
                {
                    sb.AppendLine($"Minions ({minions.Count}):");
                    foreach (var (id, info) in minions)
                    {
                        var statusIcon = info.Status switch
                        {
                            MinionStatus.Running => "●",
                            MinionStatus.Complete => "✓",
                            MinionStatus.Failed => "✗",
                            MinionStatus.Stopped => "■",
                            _ => "○"
                        };
                        var elapsed = (info.CompletedAt ?? DateTime.UtcNow) - info.StartedAt;
                        sb.AppendLine($"  {statusIcon} {id} [{info.Role}] {info.Status} ({elapsed.TotalSeconds:F0}s) - {info.Goal}");
                    }
                }

                return new ToolResult
                {
                    Success = true,
                    Output = sb.ToString(),
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
