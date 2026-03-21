# Coordinating Complex Work

For bigger jobs that involve multiple steps, the task board helps you organize work so minions can self-assign and track what's done.

## What's the Task Board?

Think of it as a shared to-do list that all minions can see. You (or the summoner) create tasks, and minions claim them one at a time. No two minions work on the same task.

## Creating Tasks

The summoner creates tasks for minions to pick up:

```
create_tasks(tasks=[
  "Refactor the UserService to use dependency injection",
  "Update all controllers to use the new UserService interface",
  "Write unit tests for the refactored UserService",
  "Update the API documentation for changed endpoints"
])
```

Each task gets an ID (task-1, task-2, etc.) and starts in the **Open** state.

## How Claiming Works

When a minion is ready for work, it checks the board:

```
task_board(action="list")
```

Then claims a task:

```
task_board(action="claim", task-id="task-1", minion-id="coder-1")
```

Once claimed, no other minion can take it. This prevents two minions from accidentally doing the same work.

## Task Dependencies

Tasks can depend on other tasks. If task-3 depends on task-1, a minion can't claim task-3 until task-1 is complete:

```
create_tasks(tasks=[...], dependsOn={"task-3": "task-1"})
```

This naturally sequences work that has to happen in order.

## Task Lifecycle

```
Open  -->  Claimed  -->  Complete
                    -->  Failed
```

| Status | Meaning |
|--------|---------|
| **Open** | Available for any minion to claim |
| **Claimed** | A minion is working on it |
| **Complete** | Done, with an optional result summary |
| **Failed** | Couldn't be completed, with an error explanation |

## Completing Tasks

When a minion finishes:

```
task_board(action="complete", task-id="task-1", result="Refactored UserService with constructor injection, updated 3 classes")
```

This unlocks any dependent tasks for other minions to claim.

## Best Practices

- **Break work into small tasks**: Each task should be completable by one minion in one session
- **Use dependencies**: If order matters, set them up front so minions don't start too early
- **Include context**: Task descriptions should be self-contained so any minion can pick them up
- **Check the board**: Ask the summoner "What's the board status?" to see overall progress
