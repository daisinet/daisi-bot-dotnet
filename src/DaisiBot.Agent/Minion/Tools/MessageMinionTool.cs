using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class MessageMinionTool : DaisiToolBase
{
    private readonly MinionProcessManager _processManager;

    public MessageMinionTool(MinionProcessManager processManager)
    {
        _processManager = processManager;
    }

    public override string Id => "summoner-message-minion";
    public override string Name => "Message Minion";

    public override string UseInstructions =>
        "Sends a structured message to a running minion. Uses the minion protocol format. " +
        "Types: directive (new instructions), answer (reply to question). " +
        "Keywords: message, send, tell, communicate, direct, answer, minion.";

    public override ToolParameter[] Parameters => [
        new() { Name = "id", Description = "The target minion ID (e.g. 'coder-1').", IsRequired = true },
        new() { Name = "type", Description = "Message type: directive (instructions) or answer (reply to question).", IsRequired = true },
        new() { Name = "content", Description = "The message content. Be specific.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var id = parameters.GetParameter("id").Value;
        var type = parameters.GetParameter("type").Value;
        var content = parameters.GetParameter("content").Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Sending {type} to {id}",
            ExecutionTask = Task.Run(() =>
            {
                var envelope = MinionProtocol.CreateMessage(type!, "summoner", content!);
                var sent = _processManager.SendMessage("summoner", id!, envelope);
                return new ToolResult
                {
                    Success = sent,
                    Output = sent ? $"[{type}] sent to '{id}'." : $"Minion '{id}' not found.",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
