using DaisiBot.Agent.Minion;

namespace DaisiBot.Agent.Tests.Minion;

public class TaskBoardTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TaskBoard _board;

    public TaskBoardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"task-board-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _board = new TaskBoard(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void CreateTask_AssignsIncrementalId()
    {
        _board.CreateTask("First task");
        _board.CreateTask("Second task");

        var tasks = _board.Tasks;
        Assert.Equal(2, tasks.Count);
        Assert.Equal("task-1", tasks[0].Id);
        Assert.Equal("task-2", tasks[1].Id);
    }

    [Fact]
    public void CreateTask_DefaultsToOpen()
    {
        _board.CreateTask("Do something");

        Assert.Equal(BoardTaskStatus.Open, _board.Tasks[0].Status);
        Assert.Null(_board.Tasks[0].Assignee);
    }

    [Fact]
    public void CreateTasks_Batch()
    {
        _board.CreateTasks(["Task A", "Task B", "Task C"]);

        Assert.Equal(3, _board.Tasks.Count);
        Assert.Equal("Task A", _board.Tasks[0].Description);
        Assert.Equal("Task C", _board.Tasks[2].Description);
    }

    [Fact]
    public void ClaimTask_TransitionsToClaimedWithAssignee()
    {
        _board.CreateTask("Fix the bug");

        var claimed = _board.ClaimTask("task-1", "coder-1");

        Assert.True(claimed);
        Assert.Equal(BoardTaskStatus.Claimed, _board.Tasks[0].Status);
        Assert.Equal("coder-1", _board.Tasks[0].Assignee);
        Assert.NotNull(_board.Tasks[0].ClaimedAt);
    }

    [Fact]
    public void ClaimTask_FailsIfAlreadyClaimed()
    {
        _board.CreateTask("Fix the bug");
        _board.ClaimTask("task-1", "coder-1");

        var claimedAgain = _board.ClaimTask("task-1", "coder-2");

        Assert.False(claimedAgain);
        Assert.Equal("coder-1", _board.Tasks[0].Assignee);
    }

    [Fact]
    public void ClaimTask_FailsForNonexistentTask()
    {
        Assert.False(_board.ClaimTask("task-999", "coder-1"));
    }

    [Fact]
    public void CompleteTask_TransitionsFromClaimed()
    {
        _board.CreateTask("Write tests");
        _board.ClaimTask("task-1", "tester-1");

        var completed = _board.CompleteTask("task-1", "All 15 tests pass.");

        Assert.True(completed);
        Assert.Equal(BoardTaskStatus.Complete, _board.Tasks[0].Status);
        Assert.Equal("All 15 tests pass.", _board.Tasks[0].Result);
        Assert.NotNull(_board.Tasks[0].CompletedAt);
    }

    [Fact]
    public void CompleteTask_FailsIfNotClaimed()
    {
        _board.CreateTask("Write tests");

        // Can't complete an Open task
        Assert.False(_board.CompleteTask("task-1"));
    }

    [Fact]
    public void FailTask_WorksFromAnyState()
    {
        _board.CreateTask("Risky task");

        var failed = _board.FailTask("task-1", "Model ran out of context.");

        Assert.True(failed);
        Assert.Equal(BoardTaskStatus.Failed, _board.Tasks[0].Status);
        Assert.Equal("Model ran out of context.", _board.Tasks[0].Result);
    }

    [Fact]
    public void Dependency_BlocksClaimUntilComplete()
    {
        _board.CreateTask("Build the API");
        _board.CreateTask("Write API tests", dependsOn: "task-1");

        // Can't claim task-2 while task-1 is Open
        Assert.False(_board.ClaimTask("task-2", "tester-1"));

        // Claim and complete task-1
        _board.ClaimTask("task-1", "coder-1");
        Assert.False(_board.ClaimTask("task-2", "tester-1")); // still blocked (Claimed, not Complete)

        _board.CompleteTask("task-1", "API built.");

        // Now task-2 is unblocked
        Assert.True(_board.ClaimTask("task-2", "tester-1"));
    }

    [Fact]
    public void Persistence_SurvivesReload()
    {
        _board.CreateTask("Persistent task");
        _board.ClaimTask("task-1", "coder-1");

        // Create a new board pointing at the same directory
        var board2 = new TaskBoard(_tempDir);

        Assert.Single(board2.Tasks);
        Assert.Equal("Persistent task", board2.Tasks[0].Description);
        Assert.Equal(BoardTaskStatus.Claimed, board2.Tasks[0].Status);
        Assert.Equal("coder-1", board2.Tasks[0].Assignee);
    }

    [Fact]
    public void FullWorkflow_MultipleMinions()
    {
        // Summoner creates tasks
        _board.CreateTasks([
            "Implement auth middleware",
            "Write auth tests",
            "Update API docs"
        ]);

        // coder-1 claims first task
        Assert.True(_board.ClaimTask("task-1", "coder-1"));

        // tester-1 claims second task
        Assert.True(_board.ClaimTask("task-2", "tester-1"));

        // writer-1 claims third task
        Assert.True(_board.ClaimTask("task-3", "writer-1"));

        // All claimed, no double-claiming
        Assert.False(_board.ClaimTask("task-1", "coder-2"));

        // coder-1 completes
        Assert.True(_board.CompleteTask("task-1", "Auth middleware added."));

        // tester-1 fails
        Assert.True(_board.FailTask("task-2", "Couldn't find test fixtures."));

        // writer-1 completes
        Assert.True(_board.CompleteTask("task-3", "Docs updated."));

        // Verify final state
        Assert.Equal(BoardTaskStatus.Complete, _board.Tasks[0].Status);
        Assert.Equal(BoardTaskStatus.Failed, _board.Tasks[1].Status);
        Assert.Equal(BoardTaskStatus.Complete, _board.Tasks[2].Status);
    }
}
