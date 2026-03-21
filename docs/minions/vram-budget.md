# Understanding Your GPU Limits

Every minion needs GPU memory for its own conversation context. The VRAM budget system figures out how many minions your hardware can support and tells you upfront.

## Why There's a Limit

When a model is loaded, the model weights live on the GPU (the "brain"). These weights are shared by all minions, which is efficient. But each minion also needs its own:

- **KV cache**: stores the conversation history so the model can reference earlier context
- **Scratch buffers**: temporary workspace for the math during generation

This per-minion memory adds up. A model that uses 4GB for weights might need an additional 300MB per minion session.

## What the Budget Message Means

When you summon, you'll see something like:

```
Max 3 minions at 2048 ctx (each ~280MB, 840MB total, 1.2GB free)
```

Breaking that down:

| Part | Meaning |
|------|---------|
| **Max 3 minions** | You can run up to 3 workers at once |
| **2048 ctx** | Each minion gets a 2048-token context window |
| **~280MB each** | Each minion session uses about 280MB of GPU memory |
| **840MB total** | All 3 minions together would use 840MB |
| **1.2GB free** | Your GPU had 1.2GB available when you summoned |

A 512MB safety reserve is always kept to prevent GPU out-of-memory errors.

## Context Size Trade-offs

Context size determines how much a minion can "remember" in a single conversation:

| Context Size | Memory Per Minion | Max Minions (8GB free) | Best For |
|-------------|-------------------|----------------------|----------|
| 1024 | ~140MB | 5-6 | Simple, focused tasks |
| 2048 | ~280MB | 3 | Most coding tasks |
| 4096 | ~560MB | 1-2 | Complex tasks needing lots of context |

**Smaller context = more minions** but each minion can hold less in memory.
**Larger context = fewer minions** but each can handle more complex reasoning.

## What Happens When You Hit the Limit

If you try to spawn more minions than the budget allows:

```
Cannot spawn: max minions (3) reached. Stop a minion first or /unsummon and re-summon with a smaller context.
```

Your options:
1. **Wait** for a running minion to finish (it frees its slot automatically)
2. **Stop** a minion that's done or stuck: `stop_minion(id="coder-1")`
3. **Unsummon and re-summon** if you want to adjust the balance

## Tips

- Close other GPU-heavy applications before summoning to maximize free VRAM
- Minions that finish their task automatically free their memory
- The budget is calculated at summon time. If VRAM changes later (other apps start/stop), the budget won't auto-adjust until you unsummon and re-summon
