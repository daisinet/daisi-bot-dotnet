using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Git
{
    public class GitStatusTool : GitToolBase
    {
        private const string P_PATH = "path";

        public override string Id => "daisi-git-status";
        public override string Name => "Daisi Git Status";

        public override string UseInstructions =>
            "Use this tool to get the git status of a repository. " +
            "Keywords: git status, repo status, working tree, modified files, staged files.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the git repository.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Getting git status: {path}",
                ExecutionTask = Execute(path, cancellation)
            };
        }

        private static async Task<ToolResult> Execute(string path, CancellationToken cancellation)
        {
            try
            {
                var (success, output) = await RunGitAsync("status", path, cancellation);
                return new ToolResult
                {
                    Success = success,
                    Output = output,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Git status retrieved",
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
