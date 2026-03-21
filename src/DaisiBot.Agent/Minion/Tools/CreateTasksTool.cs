using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class CreateTasksTool : DaisiToolBase
{
    private readonly TaskBoard _taskBoard;

    public CreateTasksTool(TaskBoard taskBoard)
    {
        _taskBoard = taskBoard;
    }

    public override string Id => "summoner-create-tasks";
    public override string Name => "Create Tasks";

    public override string UseInstructions =>
        "Creates tasks on the shared task board for minions to claim and work on. " +
        "Provide multiple task descriptions separated by newlines. " +
        "Keywords: create, tasks, board, assign, work.";

    public override ToolParameter[] Parameters => [
        new() { Name = "tasks", Description = "Task descriptions, one per line.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var tasksText = parameters.GetParameter("tasks").Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = "Creating tasks on board",
            ExecutionTask = Task.Run(() =>
            {
                var descriptions = tasksText!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _taskBoard.CreateTasks(descriptions);

                return new ToolResult
                {
                    Success = true,
                    Output = $"Created {descriptions.Length} tasks on the board.",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }
}
