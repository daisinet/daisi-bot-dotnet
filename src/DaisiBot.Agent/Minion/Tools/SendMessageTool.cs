using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class SendMessageTool : DaisiToolBase
{
    private readonly MinionMailbox _mailbox;
    private readonly string _minionId;

    public SendMessageTool(MinionMailbox mailbox, string minionId)
    {
        _mailbox = mailbox;
        _minionId = minionId;
    }

    public override string Id => "minion-send-message";
    public override string Name => "Send Message";

    public override string UseInstructions =>
        "Sends a structured message to another minion or the summoner. " +
        "Messages must use the protocol format with a type field. " +
        "Types: status (progress), blocked (need help), complete (done), failed, " +
        "question (need info), answer (reply), file_claim (claiming files), handoff (passing work). " +
        "Keywords: send, message, tell, communicate, report.";

    public override ToolParameter[] Parameters => [
        new() { Name = "to", Description = "Target: 'summoner' or a minion ID (e.g. 'coder-1').", IsRequired = true },
        new() { Name = "type", Description = "Message type: status, blocked, complete, failed, question, answer, file_claim, handoff.", IsRequired = true },
        new() { Name = "content", Description = "The message content. Be specific: include file paths, function names, error messages.", IsRequired = true },
        new() { Name = "task-id", Description = "Related task ID from the task board, if applicable.", IsRequired = false },
        new() { Name = "files", Description = "Comma-separated file paths (for file_claim or handoff).", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var to = parameters.GetParameter("to").Value;
        var type = parameters.GetParameter("type").Value;
        var content = parameters.GetParameter("content").Value;
        var taskId = parameters.GetParameter("task-id", false)?.Value;
        var filesStr = parameters.GetParameter("files", false)?.Value;
        var files = filesStr?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Sending {type} to {to}",
            ExecutionTask = Task.Run(() =>
            {
                var envelope = MinionProtocol.CreateMessage(type!, _minionId, content!, taskId, replyTo: null, files);
                _mailbox.SendMessage(_minionId, to!, envelope);
                return new ToolResult
                {
                    Success = true,
                    Output = $"[{type}] message sent to {to}.",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
