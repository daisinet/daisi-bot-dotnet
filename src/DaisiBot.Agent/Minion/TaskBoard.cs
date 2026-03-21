using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Shared task board for coordinated multi-minion work.
/// Persisted to .minion/board.json with file locking for atomic claiming.
/// </summary>
public sealed class TaskBoard
{
    private readonly string _boardPath;
    private readonly object _lock = new();
    private List<BoardTask> _tasks = [];

    public IReadOnlyList<BoardTask> Tasks
    {
        get { lock (_lock) return _tasks.ToList(); }
    }

    public TaskBoard(string workingDirectory)
    {
        var minionDir = Path.Combine(workingDirectory, ".minion");
        Directory.CreateDirectory(minionDir);
        _boardPath = Path.Combine(minionDir, "board.json");
        Load();
    }

    public void CreateTask(string description, string? assignee = null, int priority = 0, string? dependsOn = null)
    {
        lock (_lock)
        {
            var task = new BoardTask
            {
                Id = $"task-{_tasks.Count + 1}",
                Description = description,
                Status = BoardTaskStatus.Open,
                Assignee = assignee,
                Priority = priority,
                DependsOn = dependsOn,
                CreatedAt = DateTime.UtcNow
            };
            _tasks.Add(task);
            Save();
        }
    }

    public void CreateTasks(IEnumerable<string> descriptions)
    {
        lock (_lock)
        {
            foreach (var desc in descriptions)
            {
                _tasks.Add(new BoardTask
                {
                    Id = $"task-{_tasks.Count + 1}",
                    Description = desc,
                    Status = BoardTaskStatus.Open,
                    CreatedAt = DateTime.UtcNow
                });
            }
            Save();
        }
    }

    public bool ClaimTask(string taskId, string minionId)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is null || task.Status != BoardTaskStatus.Open)
                return false;

            // Check dependencies
            if (task.DependsOn is not null)
            {
                var dep = _tasks.FirstOrDefault(t => t.Id == task.DependsOn);
                if (dep is not null && dep.Status != BoardTaskStatus.Complete)
                    return false;
            }

            task.Status = BoardTaskStatus.Claimed;
            task.Assignee = minionId;
            task.ClaimedAt = DateTime.UtcNow;
            Save();
            return true;
        }
    }

    public bool CompleteTask(string taskId, string? result = null)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is null || task.Status != BoardTaskStatus.Claimed)
                return false;

            task.Status = BoardTaskStatus.Complete;
            task.Result = result;
            task.CompletedAt = DateTime.UtcNow;
            Save();
            return true;
        }
    }

    public bool FailTask(string taskId, string? error = null)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is null)
                return false;

            task.Status = BoardTaskStatus.Failed;
            task.Result = error;
            task.CompletedAt = DateTime.UtcNow;
            Save();
            return true;
        }
    }

    private void Load()
    {
        if (File.Exists(_boardPath))
        {
            try
            {
                var json = File.ReadAllText(_boardPath);
                _tasks = JsonSerializer.Deserialize<List<BoardTask>>(json) ?? [];
            }
            catch
            {
                _tasks = [];
            }
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_boardPath, json);
    }
}

public sealed class BoardTask
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("status")]
    public BoardTaskStatus Status { get; set; }

    [JsonPropertyName("assignee")]
    public string? Assignee { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("dependsOn")]
    public string? DependsOn { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("claimedAt")]
    public DateTime? ClaimedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoardTaskStatus
{
    Open,
    Claimed,
    Complete,
    Failed
}
