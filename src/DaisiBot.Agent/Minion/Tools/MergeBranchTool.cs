using System.Diagnostics;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.Agent.Minion.Tools;

public class MergeBranchTool : DaisiToolBase
{
    public override string Id => "summoner-merge-branch";
    public override string Name => "Merge Minion Branch";

    public override string UseInstructions =>
        "Merges a minion's git branch into the current branch. " +
        "Each minion works on minion/<id>/<goal-slug>. This tool merges that branch. " +
        "Keywords: merge, branch, git, minion.";

    public override ToolParameter[] Parameters => [
        new() { Name = "branch", Description = "The branch name to merge (e.g. 'minion/coder-1/fix-auth').", IsRequired = true },
        new() { Name = "strategy", Description = "Merge strategy: merge, squash, or rebase. Default is merge.", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var branch = parameters.GetParameter("branch").Value;
        var strategy = parameters.GetParameterValueOrDefault("strategy", "merge");

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Merging branch {branch}",
            ExecutionTask = Task.Run(async () =>
            {
                var args = strategy switch
                {
                    "squash" => $"merge --squash {branch}",
                    "rebase" => $"rebase {branch}",
                    _ => $"merge {branch}"
                };

                var result = await RunGitAsync(args, cancellation);
                return new ToolResult
                {
                    Success = result.ExitCode == 0,
                    Output = result.Output,
                    ErrorMessage = result.ExitCode != 0 ? result.Output : null,
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
