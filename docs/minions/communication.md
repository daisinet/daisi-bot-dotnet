# How Minions Talk

Minions and the summoner communicate through structured messages. This isn't just chat; it's a protocol that helps everyone stay coordinated.

## Message Types

| Type | Who Sends It | What It Means |
|------|-------------|---------------|
| **status** | Minion | "Here's what I've been doing" (progress update) |
| **blocked** | Minion | "I'm stuck and need help" |
| **complete** | Minion | "I finished my task" |
| **failed** | Minion | "Something went wrong and I can't continue" |
| **question** | Minion | "I need to ask something before I can proceed" |
| **answer** | Anyone | Reply to a question |
| **file_claim** | Minion | "I'm about to edit these files" (prevents conflicts) |
| **handoff** | Minion | "I'm passing this partial work to someone else" |
| **directive** | Summoner | "New instructions for you" |

## How Minions Report

Minions automatically send messages as they work:

- Every few steps, they send a **status** update
- When they finish, they send a **complete** message with a summary
- If they get stuck, they send a **blocked** message explaining why
- Before editing shared files, they send a **file_claim** message

## Sending Messages to Minions

As the summoner, you can send directives to any minion:

```
message_minion(id="coder-1", type="directive", content="Stop what you're doing and focus on the login bug first")
```

Or have the bot do it naturally. Just say: "Tell coder-1 to prioritize the login bug."

## How Minions Talk to Each Other

Minions can send messages to other minions too:

```
send_message(to="tester-1", type="answer", content="The auth module is at src/Auth/LoginService.cs")
```

This is useful when one minion has information another needs.

## What Happens When a Minion Gets Stuck

When a minion sends a **blocked** message, it means it can't make progress without help. Common reasons:

- Missing information ("I don't know which database table to use")
- Dependencies ("I need coder-1 to finish the API before I can write tests")
- Permissions ("I can't access the config file")

The summoner sees blocked messages and can either answer the question, reassign the work, or spawn a helper minion.
