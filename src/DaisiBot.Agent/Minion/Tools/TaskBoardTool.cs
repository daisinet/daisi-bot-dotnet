using System.Text;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class TaskBoardTool : DaisiToolBase
{
    private readonly TaskBoard _taskBoard;

    public TaskBoardTool(TaskBoard taskBoard)
    {
        _taskBoard = taskBoard;
    }

    public override string Id => "minion-task-board";
    public override string Name => "Task Board";

    public override string UseInstructions =>
        "Interact with the shared task board. Actions: list (show all tasks), " +
        "claim <task-id> (claim a task to work on), complete <task-id> [result] (mark done), " +
        "fail <task-id> [error] (mark failed). " +
        "Keywords: tasks, board, claim, complete, list, status.";

    public override ToolParameter[] Parameters => [
        new() { Name = "action", Description = "Action: list, claim, complete, or fail.", IsRequired = true },
        new() { Name = "task-id", Description = "The task ID (e.g. 'task-1'). Required for claim/complete/fail.", IsRequired = false },
        new() { Name = "minion-id", Description = "The minion ID claiming the task. Required for claim.", IsRequired = false },
        new() { Name = "result", Description = "Result text for complete/fail.", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameter("action").Value?.ToLower();
        var taskId = parameters.GetParameter("task-id", false)?.Value;
        var minionId = parameters.GetParameter("minion-id", false)?.Value;
        var result = parameters.GetParameter("result", false)?.Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Task board: {action}",
            ExecutionTask = Task.Run(() =>
            {
                return action switch
                {
                    "list" => ListTasks(),
                    "claim" => ClaimTask(taskId!, minionId!),
                    "complete" => CompleteTask(taskId!, result),
                    "fail" => FailTask(taskId!, result),
                    _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use list, claim, complete, or fail." }
                };
            }, cancellation)
        };
    }

    private ToolResult ListTasks()
    {
        var tasks = _taskBoard.Tasks;
        if (tasks.Count == 0)
            return new ToolResult { Success = true, Output = "No tasks on the board.", OutputFormat = InferenceOutputFormats.PlainText };

        var sb = new StringBuilder();
        sb.AppendLine($"Task Board ({tasks.Count} tasks):");
        foreach (var task in tasks)
        {
            var icon = task.Status switch
            {
                BoardTaskStatus.Open => "○",
                BoardTaskStatus.Claimed => "●",
                BoardTaskStatus.Complete => "✓",
                BoardTaskStatus.Failed => "✗"
            };
            var assignee = task.Assignee is not null ? $" [{task.Assignee}]" : "";
            sb.AppendLine($"  {icon} {task.Id}: {task.Description}{assignee} ({task.Status})");
        }

        return new ToolResult { Success = true, Output = sb.ToString(), OutputFormat = InferenceOutputFormats.PlainText };
    }

    private ToolResult ClaimTask(string taskId, string minionId)
    {
        if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(minionId))
            return new ToolResult { Success = false, ErrorMessage = "task-id and minion-id are required for claim." };

        var claimed = _taskBoard.ClaimTask(taskId, minionId);
        return new ToolResult
        {
            Success = claimed,
            Output = claimed ? $"Task {taskId} claimed by {minionId}." : $"Could not claim {taskId} (not open or has unmet dependencies).",
            OutputFormat = InferenceOutputFormats.PlainText
        };
    }

    private ToolResult CompleteTask(string taskId, string? result)
    {
        if (string.IsNullOrEmpty(taskId))
            return new ToolResult { Success = false, ErrorMessage = "task-id is required for complete." };

        var completed = _taskBoard.CompleteTask(taskId, result);
        return new ToolResult
        {
            Success = completed,
            Output = completed ? $"Task {taskId} completed." : $"Could not complete {taskId} (not claimed).",
            OutputFormat = InferenceOutputFormats.PlainText
        };
    }

    private ToolResult FailTask(string taskId, string? error)
    {
        if (string.IsNullOrEmpty(taskId))
            return new ToolResult { Success = false, ErrorMessage = "task-id is required for fail." };

        var failed = _taskBoard.FailTask(taskId, error);
        return new ToolResult
        {
            Success = failed,
            Output = failed ? $"Task {taskId} marked as failed." : $"Task {taskId} not found.",
            OutputFormat = InferenceOutputFormats.PlainText
        };
    }
}
