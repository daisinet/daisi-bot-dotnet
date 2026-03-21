# Watching Your Team

Once minions are running, you'll want to keep an eye on their progress. There are several ways to check what's happening.

## Quick Status Check

The simplest way to see all minions:

```
/minions
```

This shows a compact list:

```
Minions (2/3 distributed, 2048 ctx each):
  ● coder-1 [coder] Running (45s) - Fix the auth bug in LoginService.cs
  ✓ tester-1 [tester] Complete (120s) - Write tests for the auth module
```

### Status Icons

| Icon | Status | Meaning |
|------|--------|---------|
| ● | Running | Minion is actively working |
| ✓ | Complete | Minion finished its task |
| ✗ | Failed | Something went wrong |
| ■ | Stopped | Manually stopped by you |
| ○ | Starting | Minion is initializing |

## Checking a Specific Minion

To see what a minion has been doing, use the `check_minion` tool:

```
check_minion(id="coder-1")
```

This returns the minion's recent output, including what it's been thinking, which files it's edited, and any messages it's sent.

You can control how much output to see with the `tail` parameter:

```
check_minion(id="coder-1", tail=100)
```

## Stopping a Minion

If a minion is stuck or you want to reassign the work:

```
stop_minion(id="coder-1")
```

Or stop everyone:

```
stop_minion(id="all")
```

Stopping a minion frees its GPU memory slot, letting you spawn a new one.

## Reading Minion Messages

Minions send structured messages about their progress. The summoner (your bot) receives these automatically, but you can also ask:

- "What have my minions reported?"
- "Is coder-1 done yet?"
- "Are any minions blocked?"

The bot will check the latest messages and summarize for you.
