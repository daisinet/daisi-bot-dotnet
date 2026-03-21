using System.Text;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class CheckMinionTool : DaisiToolBase
{
    private readonly MinionProcessManager _processManager;

    public CheckMinionTool(MinionProcessManager processManager)
    {
        _processManager = processManager;
    }

    public override string Id => "summoner-check-minion";
    public override string Name => "Check Minion";

    public override string UseInstructions =>
        "Gets the current output and status from a specific minion. " +
        "Keywords: check, output, progress, minion, result.";

    public override ToolParameter[] Parameters => [
        new() { Name = "id", Description = "The minion ID (e.g. 'coder-1').", IsRequired = true },
        new() { Name = "tail", Description = "Number of lines from the end to return. Default is 50.", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var id = parameters.GetParameter("id").Value;
        var tailStr = parameters.GetParameterValueOrDefault("tail", "50");
        if (!int.TryParse(tailStr, out var tailLines)) tailLines = 50;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Checking minion {id}",
            ExecutionTask = Task.Run(() =>
            {
                var status = _processManager.GetMinionStatus(id);
                if (status is null)
                    return new ToolResult { Success = false, ErrorMessage = $"Minion '{id}' not found." };

                var output = _processManager.GetMinionOutput(id) ?? "(no output)";

                // Tail the output
                var lines = output.Split('\n');
                if (lines.Length > tailLines)
                    output = string.Join('\n', lines[^tailLines..]);

                var sb = new StringBuilder();
                sb.AppendLine($"Minion: {id}");
                sb.AppendLine($"Status: {status}");
                sb.AppendLine($"--- Output (last {tailLines} lines) ---");
                sb.Append(output);

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
