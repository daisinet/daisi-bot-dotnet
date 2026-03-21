using System.Text;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class ReadMessagesTool : DaisiToolBase
{
    private readonly MinionMailbox _mailbox;
    private readonly string _minionId;

    public ReadMessagesTool(MinionMailbox mailbox, string minionId)
    {
        _mailbox = mailbox;
        _minionId = minionId;
    }

    public override string Id => "minion-read-messages";
    public override string Name => "Read Messages";

    public override string UseInstructions =>
        "Reads messages from your inbox. Messages are cleared after reading. " +
        "Check your inbox periodically between major steps for directives from the summoner " +
        "or questions from other minions. Messages use the protocol format with a type field. " +
        "Keywords: read, messages, inbox, check, directives.";

    public override ToolParameter[] Parameters => [];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        return new ToolExecutionContext
        {
            ExecutionMessage = "Reading messages",
            ExecutionTask = Task.Run(() =>
            {
                var rawMessages = _mailbox.ReadMessages(_minionId);
                if (rawMessages.Count == 0)
                    return new ToolResult { Success = true, Output = "No new messages.", OutputFormat = InferenceOutputFormats.PlainText };

                var sb = new StringBuilder();
                sb.AppendLine($"{rawMessages.Count} message(s):");
                foreach (var raw in rawMessages)
                {
                    var parsed = MinionProtocol.ParseMessage(raw.Content);
                    if (parsed is not null && parsed.Type != "text")
                    {
                        sb.AppendLine($"  [{parsed.Type}] from {parsed.From} ({parsed.Timestamp:HH:mm:ss}):");
                        sb.AppendLine($"    {parsed.Content}");
                        if (parsed.TaskId is not null)
                            sb.AppendLine($"    Task: {parsed.TaskId}");
                        if (parsed.Files is { Length: > 0 })
                            sb.AppendLine($"    Files: {string.Join(", ", parsed.Files)}");
                    }
                    else
                    {
                        // Plain text fallback
                        sb.AppendLine($"  From {raw.FromMinionId} ({raw.Timestamp:HH:mm:ss}): {raw.Content}");
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
