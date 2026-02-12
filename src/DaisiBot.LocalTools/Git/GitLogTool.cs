using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Git
{
    public class GitLogTool : GitToolBase
    {
        private const string P_PATH = "path";
        private const string P_MAX_COUNT = "max-count";
        private const string P_FORMAT = "format";

        public override string Id => "daisi-git-log";
        public override string Name => "Daisi Git Log";

        public override string UseInstructions =>
            "Use this tool to show git commit history. " +
            "Keywords: git log, commit history, recent commits, changelog.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the git repository.", IsRequired = true },
            new() { Name = P_MAX_COUNT, Description = "Maximum number of commits to show. Default is 10.", IsRequired = false },
            new() { Name = P_FORMAT, Description = "Output format string for git log --format. Default is oneline.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var maxCountStr = parameters.GetParameterValueOrDefault(P_MAX_COUNT, "10");
            var format = parameters.GetParameterValueOrDefault(P_FORMAT, "oneline");
            if (!int.TryParse(maxCountStr, out var maxCount)) maxCount = 10;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Getting git log",
                ExecutionTask = Execute(path, maxCount, format, cancellation)
            };
        }

        private static async Task<ToolResult> Execute(string path, int maxCount, string format, CancellationToken cancellation)
        {
            try
            {
                var args = $"log --max-count={maxCount} --format={format}";
                var (success, output) = await RunGitAsync(args, path, cancellation);
                return new ToolResult
                {
                    Success = success,
                    Output = string.IsNullOrEmpty(output) ? "No commits found." : output,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Git log retrieved",
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
