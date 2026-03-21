# Parallel Code Changes

When multiple minions edit code at the same time, they need to avoid stepping on each other. The minion system uses git branches and file claiming to keep things organized.

## How Minions Work on Branches

In centralized mode, each minion process can work on its own git branch. The summoner coordinates merging when minions finish.

In distributed mode, minions share the same working directory, so file claiming becomes more important to avoid conflicts.

## File Claiming

Before a minion edits a file, it should announce its intent:

```
send_message(to="summoner", type="file_claim", content="Claiming src/Auth/LoginService.cs", files=["src/Auth/LoginService.cs"])
```

The summoner tracks these claims and warns if two minions try to edit the same file:

```
message_minion(id="tester-1", type="directive", content="coder-1 is already editing LoginService.cs. Wait for them to finish.")
```

## Merging Minion Work

When minions work on separate branches, the summoner can merge their changes:

```
merge_branch(source="coder-1-branch", target="feature-branch")
```

The merge tool checks for conflicts first. If there are conflicts, it reports them so you can decide how to resolve them.

## Conflict Detection

Before merging, you can check if branches will conflict:

```
check_conflicts(branch1="coder-1-branch", branch2="tester-1-branch")
```

This lists any files modified by both branches.

## Best Practices

- **Assign non-overlapping files**: Give each minion a distinct part of the codebase to work on
- **Use file_claim**: Even in distributed mode, claiming files helps the summoner prevent conflicts
- **Merge incrementally**: Don't wait for all minions to finish. Merge completed work as it arrives so later minions can build on it
- **Review before merging**: Have a reviewer minion check changes before the summoner merges them
- **Small, focused tasks**: The narrower each minion's scope, the less likely they'll conflict
