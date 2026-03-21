using System.Diagnostics;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class CheckConflictsTool : DaisiToolBase
{
    public override string Id => "summoner-check-conflicts";
    public override string Name => "Check Conflicts";

    public override string UseInstructions =>
        "Checks if a minion's branch has merge conflicts with the current branch. " +
        "Does a dry-run merge to detect conflicts without actually merging. " +
        "Keywords: conflicts, merge, check, branch, git.";

    public override ToolParameter[] Parameters => [
        new() { Name = "branch", Description = "The branch name to check (e.g. 'minion/coder-1/fix-auth').", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var branch = parameters.GetParameter("branch").Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Checking conflicts with {branch}",
            ExecutionTask = Task.Run(async () =>
            {
                // Use git merge-tree to check for conflicts without touching the working tree
                var currentBranch = await RunGitAsync("rev-parse HEAD", cancellation);
                var targetBranch = await RunGitAsync($"rev-parse {branch}", cancellation);
                var mergeBase = await RunGitAsync($"merge-base HEAD {branch}", cancellation);

                if (currentBranch.ExitCode != 0 || targetBranch.ExitCode != 0 || mergeBase.ExitCode != 0)
                {
                    return new ToolResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to resolve branches. Make sure '{branch}' exists.",
                        OutputFormat = InferenceOutputFormats.PlainText
                    };
                }

                var mergeTree = await RunGitAsync(
                    $"merge-tree {mergeBase.Output.Trim()} {currentBranch.Output.Trim()} {targetBranch.Output.Trim()}",
                    cancellation);

                var hasConflicts = mergeTree.Output.Contains("<<<<<<") ||
                                   mergeTree.Output.Contains("changed in both");

                if (hasConflicts)
                {
                    return new ToolResult
                    {
                        Success = true,
                        Output = $"CONFLICTS DETECTED merging {branch}:\n{mergeTree.Output}",
                        OutputFormat = InferenceOutputFormats.PlainText
                    };
                }

                return new ToolResult
                {
                    Success = true,
                    Output = $"No conflicts. Branch {branch} can be merged cleanly.",
                    OutputFormat = InferenceOutputFormats.PlainText
                };
            }, cancellation)
        };
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = stdout;
        if (!string.IsNullOrEmpty(stderr))
            output += (string.IsNullOrEmpty(output) ? "" : "\n") + stderr;

        return (process.ExitCode, output);
    }
}
