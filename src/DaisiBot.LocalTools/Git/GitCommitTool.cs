using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Git
{
    public class GitCommitTool : GitToolBase
    {
        private const string P_PATH = "path";
        private const string P_MESSAGE = "message";
        private const string P_FILES = "files";

        public override string Id => "daisi-git-commit";
        public override string Name => "Daisi Git Commit";

        public override string UseInstructions =>
            "Use this tool to stage files and create a git commit. " +
            "Keywords: git commit, save changes, commit code.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the git repository.", IsRequired = true },
            new() { Name = P_MESSAGE, Description = "Commit message.", IsRequired = true },
            new() { Name = P_FILES, Description = "Comma-separated list of files to stage. If omitted, stages all changes.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var message = parameters.GetParameter(P_MESSAGE).Value;
            var files = parameters.GetParameter(P_FILES, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Creating git commit",
                ExecutionTask = Execute(path, message, files, cancellation)
            };
        }

        private static async Task<ToolResult> Execute(string path, string message, string? files, CancellationToken cancellation)
        {
            try
            {
                string addArgs;
                if (!string.IsNullOrEmpty(files))
                {
                    var fileList = files.Split(',').Select(f => $"\"{f.Trim()}\"");
                    addArgs = $"add {string.Join(" ", fileList)}";
                }
                else
                {
                    addArgs = "add -A";
                }

                var (addSuccess, addOutput) = await RunGitAsync(addArgs, path, cancellation);
                if (!addSuccess)
                    return new ToolResult { Success = false, ErrorMessage = $"Failed to stage files: {addOutput}" };

                var (commitSuccess, commitOutput) = await RunGitAsync($"commit -m \"{message.Replace("\"", "\\\"")}\"", path, cancellation);
                return new ToolResult
                {
                    Success = commitSuccess,
                    Output = commitOutput,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = commitSuccess ? "Commit created" : "Commit failed",
                    ErrorMessage = commitSuccess ? null : commitOutput
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
