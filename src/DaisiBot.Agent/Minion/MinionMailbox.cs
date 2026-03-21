using System.Text.Json;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// File-based message passing between minions.
/// Each minion has an inbox at .minion/{id}/inbox.json.
/// </summary>
public sealed class MinionMailbox
{
    private readonly string _workingDirectory;

    public MinionMailbox(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public void SendMessage(string fromId, string toId, string content)
    {
        var inboxDir = Path.Combine(_workingDirectory, ".minion", toId);
        Directory.CreateDirectory(inboxDir);
        var inboxPath = Path.Combine(inboxDir, "inbox.json");

        var message = new MailboxMessage
        {
            FromMinionId = fromId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(message);

        // Append with file lock
        using var fs = new FileStream(inboxPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var sw = new StreamWriter(fs);
        sw.WriteLine(json);
    }

    public List<MailboxMessage> ReadMessages(string minionId, bool clear = true)
    {
        var inboxPath = Path.Combine(_workingDirectory, ".minion", minionId, "inbox.json");
        if (!File.Exists(inboxPath))
            return [];

        var messages = new List<MailboxMessage>();
        var lines = File.ReadAllLines(inboxPath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var msg = JsonSerializer.Deserialize<MailboxMessage>(line);
                if (msg is not null) messages.Add(msg);
            }
            catch { }
        }

        if (clear)
            File.WriteAllText(inboxPath, "");

        return messages;
    }
}

public sealed class MailboxMessage
{
    public string FromMinionId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
