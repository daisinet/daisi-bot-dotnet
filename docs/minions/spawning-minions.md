# Creating Workers

Once you've summoned, you can spawn minions to work on specific tasks. Each minion is an autonomous worker with a role and a goal.

## How to Spawn

```
/spawn <role> <goal>
```

### Examples

```
/spawn coder "Fix the authentication bug in LoginService.cs"
/spawn tester "Write integration tests for the payment API"
/spawn researcher "Find all places where we use deprecated API v1 endpoints"
/spawn writer "Write user documentation for the new export feature"
/spawn reviewer "Review the changes in the auth module for security issues"
```

## Roles

Roles help minions understand what kind of work to focus on:

| Role | Best For |
|------|----------|
| **coder** | Writing, fixing, and refactoring code |
| **tester** | Writing tests and running test suites |
| **researcher** | Reading code, finding patterns, answering questions |
| **writer** | Documentation, comments, README files |
| **reviewer** | Code review, security audit, quality checks |

You can use any role name you like. The built-in roles come with tailored system prompts, but custom roles work too.

## Writing Good Goals

Be specific. A minion works best when it knows exactly what to do.

**Good goals:**
- "Fix the null reference exception in UserService.GetProfile when user has no avatar"
- "Add input validation to all POST endpoints in the OrderController"
- "Write unit tests for the CartService.CalculateTotal method covering edge cases"

**Less effective goals:**
- "Fix bugs" (too vague)
- "Make the code better" (no clear success criteria)
- "Do everything" (too broad for one minion)

## How Many Minions Can You Run?

The number depends on your GPU memory. When you summon, the bot tells you the limit:

```
Summoned (distributed)! Max 3 minions at 2048 ctx
```

If you try to spawn beyond the limit:

```
Cannot spawn: max minions (3) reached. Stop a minion first or /unsummon and re-summon with a smaller context.
```

To free a slot, you can stop a completed or stuck minion, or wait for one to finish.

## What Happens After Spawning

The minion starts working immediately:

1. It receives your goal as its first instruction
2. It begins working autonomously (reading files, writing code, using tools)
3. It sends periodic status updates
4. When done, it sends a completion message
5. Its GPU memory is freed for the next minion

You don't need to babysit minions. Check in when you want with `/minions` or the `check_minion` tool.
