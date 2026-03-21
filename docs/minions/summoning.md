# Becoming a Summoner

Summoning transforms your bot from a single assistant into a team coordinator. Once summoned, you can create worker minions that handle tasks in parallel while you oversee the big picture.

## How to Summon

```
/summon
```

That's it. The bot checks your GPU, calculates how many minions you can run, and activates team mode. You'll see a message like:

```
Summoned (distributed)! Max 3 minions at 2048 ctx (each ~280MB, 840MB total, 1.2GB free)
```

This tells you:
- **Max 3 minions**: how many workers you can run at once
- **2048 ctx**: each minion's context window size (how much it can "remember")
- **~280MB each**: how much GPU memory each minion uses
- **1.2GB free**: how much GPU memory was available

## Two Modes

### Distributed Mode (Default)

The default mode runs minions as lightweight tasks inside the same process. They share the model's brain directly with zero network overhead. This is faster and simpler.

```
/summon
```

### Centralized Mode

The original mode that runs minions as separate processes connected via gRPC. Use this if you need process isolation or are running on CPU only.

```
/summon --centralized
```

Or with a custom port:
```
/summon --centralized 50052
```

## How to Unsummon

When you're done with team mode:

```
/unsummon
```

This stops all running minions, frees their GPU memory, and returns your bot to normal single-assistant mode. Any work the minions completed (files they edited, branches they created) remains.

## When to Use Which Mode

| Situation | Mode |
|-----------|------|
| You have a GPU and want speed | Distributed (default) |
| You want maximum simplicity | Distributed (default) |
| You're running on CPU only | Centralized |
| You need process-level isolation | Centralized |
| You're debugging minion issues | Centralized (separate logs per process) |

## What Happens to Your Bot

While summoned, your bot still works normally. You can chat with it, ask it questions, and use it like always. The difference is that it now has access to team coordination tools (spawning minions, checking their status, sending them messages).

When you or the bot generate a response, the GPU is shared fairly. If a minion is mid-thought when you send a message, it pauses briefly while your response generates, then resumes. You'll barely notice.
