using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Defines the structured message protocol for minion-to-minion and minion-to-summoner communication.
/// All messages are JSON envelopes with a type field so receivers can route programmatically.
/// </summary>
public static class MinionProtocol
{
    // ── Message Types ──

    /// <summary>Minion reporting progress on its current task.</summary>
    public const string TypeStatus = "status";

    /// <summary>Minion is blocked and needs help or a decision.</summary>
    public const string TypeBlocked = "blocked";

    /// <summary>Minion finished its task successfully.</summary>
    public const string TypeComplete = "complete";

    /// <summary>Minion failed its task.</summary>
    public const string TypeFailed = "failed";

    /// <summary>Minion asking a question to summoner or another minion.</summary>
    public const string TypeQuestion = "question";

    /// <summary>Answer to a previously asked question.</summary>
    public const string TypeAnswer = "answer";

    /// <summary>Minion announcing it modified a file (claim/lock signal).</summary>
    public const string TypeFileClaim = "file_claim";

    /// <summary>Handoff: minion passing partial work to another minion.</summary>
    public const string TypeHandoff = "handoff";

    /// <summary>Summoner directive: new instructions or priority change.</summary>
    public const string TypeDirective = "directive";

    // ── Envelope ──

    public static string CreateMessage(string type, string from, string content, string? taskId = null, string? replyTo = null, string[]? files = null)
    {
        var envelope = new ProtocolMessage
        {
            Type = type,
            From = from,
            Content = content,
            TaskId = taskId,
            ReplyTo = replyTo,
            Files = files,
            Timestamp = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(envelope);
    }

    public static ProtocolMessage? ParseMessage(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProtocolMessage>(json);
        }
        catch
        {
            // Fall back to treating it as a plain-text message
            return new ProtocolMessage
            {
                Type = "text",
                From = "unknown",
                Content = json,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    // ── System Prompt Fragment ──

    /// <summary>
    /// Returns the protocol instructions to inject into every minion's system prompt.
    /// This teaches the LLM how to communicate with the summoner and other minions.
    /// </summary>
    public static string GetMinionProtocolPrompt(string minionId, string role, string? summonerInfo = null)
    {
        return $$"""
            ## Minion Communication Protocol

            You are minion `{{minionId}}` with role `{{role}}`. You are part of a multi-minion team coordinated by a summoner.

            ### Message Format
            When using send_message, always use the `type` parameter to specify the message type.
            Available types: status, blocked, complete, failed, question, answer, file_claim, handoff.

            Example: send_message(to="summoner", type="complete", content="Finished implementing auth. Files: src/Auth.cs, tests/AuthTests.cs")

            ### Message Types
            | Type | When to Send |
            |------|-------------|
            | `status` | Every 2-3 significant steps — progress update |
            | `blocked` | When you can't proceed — missing info, dependency not ready |
            | `complete` | When your task/goal is done — summary of what you did |
            | `failed` | When your task cannot be completed — what went wrong |
            | `question` | When you need info from summoner or another minion |
            | `answer` | Replying to a question from another minion |
            | `file_claim` | Before modifying shared files — announce which files |
            | `handoff` | Passing partial work to another minion |

            ### Rules
            1. **Always report completion** — when your goal is done, send a `complete` message to `summoner`
            2. **Report blockers immediately** — don't spin; send a `blocked` message so the summoner can help
            3. **Claim files before editing** — if you know another minion might touch the same files, send `file_claim` first
            4. **Check messages periodically** — use read_messages between major steps to see if the summoner has new directives
            5. **Check the task board** — if you were spawned for board work, claim a task before starting, complete it when done
            6. **Be specific** — include file paths, function names, error messages. The summoner and other minions can't see your screen.

            ### Addressing
            - Send to `summoner` for the coordinator
            - Send to a specific minion ID (e.g. `coder-1`, `tester-1`) for peer communication
            - The summoner sees all; you only see messages addressed to you
            """;
    }

    /// <summary>
    /// Returns the protocol instructions for the summoner's system prompt.
    /// </summary>
    public static string GetSummonerProtocolPrompt()
    {
        return """
            ## Summoner Communication Protocol

            You coordinate a team of minions. They communicate using structured JSON messages.

            ### Message Types You'll Receive
            | Type | Meaning | Suggested Response |
            |------|---------|-------------------|
            | `status` | Minion progress update | Acknowledge or redirect if off-track |
            | `blocked` | Minion can't proceed | Provide the missing info, reassign, or spawn a helper |
            | `complete` | Minion finished its task | Check output quality, assign next task or merge branch |
            | `failed` | Minion's task failed | Diagnose, provide guidance, or reassign to a different minion |
            | `question` | Minion needs info | Answer directly via message_minion |
            | `file_claim` | Minion claiming files | Track to prevent conflicts; warn other minions if overlap |

            ### Message Types You Send
            | Type | When | Format |
            |------|------|--------|
            | `directive` | New instructions or priority change | `{"type": "directive", "from": "summoner", "content": "..."}` |
            | `answer` | Replying to a minion's question | `{"type": "answer", "from": "summoner", "content": "...", "replyTo": "<minion-id>"}` |

            ### Coordination Strategy
            1. **Monitor actively** — check_minion on running minions regularly
            2. **Respond to blockers fast** — a blocked minion is wasting inference time
            3. **Track file ownership** — if two minions claim overlapping files, send a directive to one
            4. **Merge incrementally** — don't wait for all minions; merge completed work as it arrives
            5. **Use the task board** — for complex multi-step work, create tasks and let minions self-organize
            """;
    }
}

public sealed class ProtocolMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("taskId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaskId { get; set; }

    [JsonPropertyName("replyTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyTo { get; set; }

    [JsonPropertyName("files")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Files { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
