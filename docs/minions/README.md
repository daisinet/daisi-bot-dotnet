# Daisi Bot Multi-Minion System

The multi-minion system lets your bot split into a coordinated team of AI workers. One bot becomes the **summoner** (the coordinator), and it can create **minions** (the workers) that each tackle a specific task in parallel.

Think of it like a team lead delegating work: you tell the summoner what needs to happen, and it assigns specialized minions to handle different parts of the job.

## Quick Start

1. **Load a model** in your bot (the bot needs a model running to create minions)
2. **Summon** to activate team mode:
   ```
   /summon
   ```
3. **Spawn workers** for specific tasks:
   ```
   /spawn coder "Fix the login bug in auth.cs"
   /spawn tester "Write unit tests for the auth module"
   ```
4. **Watch them work** with the dashboard or:
   ```
   /minions
   ```
5. **Unsummon** when you're done:
   ```
   /unsummon
   ```

## How It Works

When you summon, the bot checks your GPU memory and figures out how many minions it can run. Each minion gets its own conversation context (like its own scratchpad) while sharing the brain (the model weights) with everyone else.

Minions work autonomously: they chat with the model, use tools, report progress, and signal when they're done or stuck. The summoner coordinates everything and you stay in control.

## Guides

| Guide | What You'll Learn |
|-------|------------------|
| [Becoming a Summoner](summoning.md) | How to activate and deactivate team mode |
| [Creating Workers](spawning-minions.md) | How to spawn minions with roles and goals |
| [Watching Your Team](minion-dashboard.md) | How to monitor minion progress |
| [How Minions Talk](communication.md) | The message system between minions and summoner |
| [Coordinating Work](task-board.md) | Using the task board for complex multi-step jobs |
| [GPU Limits](vram-budget.md) | Understanding your hardware limits |
| [Parallel Code Changes](git-branches.md) | How minions work on separate branches |
