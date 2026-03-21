using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class StopMinionTool : DaisiToolBase
{
    private readonly MinionProcessManager _processManager;

    public StopMinionTool(MinionProcessManager processManager)
    {
        _processManager = processManager;
    }

    public override string Id => "summoner-stop-minion";
    public override string Name => "Stop Minion";

    public override string UseInstructions =>
        "Stops a running minion process. " +
        "Keywords: stop, kill, terminate, minion.";

    public override ToolParameter[] Parameters => [
        new() { Name = "id", Description = "The minion ID to stop (e.g. 'coder-1'). Use 'all' to stop all minions.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var id = parameters.GetParameter("id").Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Stopping minion {id}",
            ExecutionTask = Task.Run(() =>
            {
                if (id == "all")
                {
                    _processManager.StopAll();
                    return new ToolResult
                    {
                        Success = true,
                        Output = "All minions stopped.",
                        OutputFormat = InferenceOutputFormats.PlainText
                    };
                }

                var stopped = _processManager.StopMinion(id);
                return new ToolResult
                {
                    Success = stopped,
                    Output = stopped ? $"Minion '{id}' stopped." : $"Minion '{id}' not found or already stopped.",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
