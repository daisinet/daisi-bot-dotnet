using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Git
{
    public class GitBranchTool : GitToolBase
    {
        private const string P_PATH = "path";
        private const string P_ACTION = "action";
        private const string P_NAME = "name";

        public override string Id => "daisi-git-branch";
        public override string Name => "Daisi Git Branch";

        public override string UseInstructions =>
            "Use this tool to manage git branches: list, create, checkout, or delete. " +
            "Keywords: git branch, create branch, switch branch, list branches, checkout branch.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the git repository.", IsRequired = true },
            new() { Name = P_ACTION, Description = "Action: list, create, checkout, or delete. Default is list.", IsRequired = false },
            new() { Name = P_NAME, Description = "Branch name (required for create, checkout, delete).", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var action = parameters.GetParameterValueOrDefault(P_ACTION, "list");
            var name = parameters.GetParameter(P_NAME, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Git branch {action}",
                ExecutionTask = Execute(path, action, name, cancellation)
            };
        }

        private static async Task<ToolResult> Execute(string path, string action, string? name, CancellationToken cancellation)
        {
            try
            {
                var args = action.ToLower() switch
                {
                    "create" => !string.IsNullOrEmpty(name) ? $"branch \"{name}\"" : null,
                    "checkout" => !string.IsNullOrEmpty(name) ? $"checkout \"{name}\"" : null,
                    "delete" => !string.IsNullOrEmpty(name) ? $"branch -d \"{name}\"" : null,
                    _ => "branch"
                };

                if (args == null)
                    return new ToolResult { Success = false, ErrorMessage = $"Branch name is required for {action} action." };

                var (success, output) = await RunGitAsync(args, path, cancellation);
                return new ToolResult
                {
                    Success = success,
                    Output = output,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Git branch {action} completed",
                    ErrorMessage = success ? null : output
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
