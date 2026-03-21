using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class SpawnMinionTool : DaisiToolBase
{
    private readonly MinionProcessManager _processManager;
    private readonly int _serverPort;

    public SpawnMinionTool(MinionProcessManager processManager, int serverPort)
    {
        _processManager = processManager;
        _serverPort = serverPort;
    }

    public override string Id => "summoner-spawn-minion";
    public override string Name => "Spawn Minion";

    public override string UseInstructions =>
        "Spawns a new headless minion worker with a specific role and goal. " +
        "The minion connects to this summoner's inference server and works autonomously. " +
        "Keywords: spawn, create, launch, minion, worker, agent.";

    public override ToolParameter[] Parameters => [
        new() { Name = "role", Description = "The role for the minion: coder, tester, researcher, writer, reviewer.", IsRequired = true },
        new() { Name = "goal", Description = "A clear, specific goal for the minion to accomplish.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var role = parameters.GetParameter("role").Value;
        var goal = parameters.GetParameter("goal").Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Spawning {role} minion: {goal}",
            ExecutionTask = Task.Run(() =>
            {
                var info = _processManager.SpawnMinion(role, goal, _serverPort);
                return new ToolResult
                {
                    Success = info.Status != MinionStatus.Failed,
                    Output = $"Spawned minion '{info.Id}' as {role}.\nGoal: {goal}\nOutput dir: {info.OutputDir}",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
