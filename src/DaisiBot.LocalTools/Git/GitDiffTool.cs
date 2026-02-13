using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Git
{
    public class GitDiffTool : GitToolBase
    {
        private const string P_PATH = "path";
        private const string P_STAGED = "staged";
        private const string P_FILE = "file";

        public override string Id => "daisi-git-diff";
        public override string Name => "Daisi Git Diff";

        public override string UseInstructions =>
            "Use this tool to show git differences. Can show staged or unstaged changes. " +
            "Keywords: git diff, changes, differences, what changed.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the git repository.", IsRequired = true },
            new() { Name = P_STAGED, Description = "If true, show staged changes. Default is false.", IsRequired = false },
            new() { Name = P_FILE, Description = "Specific file to diff.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var staged = parameters.GetParameterValueOrDefault(P_STAGED, "false");
            var file = parameters.GetParameter(P_FILE, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Getting git diff",
                ExecutionTask = Execute(path, staged, file, cancellation)
            };
        }

        private static async Task<ToolResult> Execute(string path, string staged, string? file, CancellationToken cancellation)
        {
            try
            {
                var args = "diff";
                if (staged.ToLower() is "true" or "yes" or "1")
                    args += " --staged";
                if (!string.IsNullOrEmpty(file))
                    args += $" -- \"{file}\"";

                var (success, output) = await RunGitAsync(args, path, cancellation);
                return new ToolResult
                {
                    Success = success,
                    Output = string.IsNullOrEmpty(output) ? "No changes found." : output,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Git diff retrieved",
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
