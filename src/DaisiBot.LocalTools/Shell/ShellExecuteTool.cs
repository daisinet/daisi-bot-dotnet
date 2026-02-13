using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.Shell
{
    public class ShellExecuteTool : DaisiToolBase
    {
        private const string P_COMMAND = "command";
        private const string P_SHELL = "shell";
        private const string P_WORKING_DIR = "working-directory";
        private const string P_TIMEOUT = "timeout";
        private const int MaxOutputBytes = 102_400;

        public override string Id => "daisi-shell-execute";
        public override string Name => "Daisi Shell Execute";

        public override string UseInstructions =>
            "Use this tool to execute a shell command and return its output. " +
            "Supports cmd, powershell, and bash shells. Output is capped at 100KB. " +
            "Keywords: run command, execute, shell, terminal, cmd, powershell, bash.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_COMMAND, Description = "The command to execute.", IsRequired = true },
            new() { Name = P_SHELL, Description = "Shell to use: cmd, powershell, or bash. Default is cmd.", IsRequired = false },
            new() { Name = P_WORKING_DIR, Description = "Working directory for the command.", IsRequired = false },
            new() { Name = P_TIMEOUT, Description = "Timeout in seconds. Default is 30.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var command = parameters.GetParameter(P_COMMAND).Value;
            var shell = parameters.GetParameterValueOrDefault(P_SHELL, "cmd");
            var workDir = parameters.GetParameter(P_WORKING_DIR, false)?.Value;
            var timeoutStr = parameters.GetParameterValueOrDefault(P_TIMEOUT, "30");
            if (!int.TryParse(timeoutStr, out var timeoutSec)) timeoutSec = 30;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Executing shell command: {command}",
                ExecutionTask = ExecuteCommand(command, shell, workDir, timeoutSec, cancellation)
            };
        }

        private static async Task<ToolResult> ExecuteCommand(string command, string shell, string? workDir, int timeoutSec, CancellationToken cancellation)
        {
            try
            {
                var (fileName, arguments) = shell.ToLower() switch
                {
                    "powershell" => ("powershell", $"-NoProfile -Command \"{command}\""),
                    "bash" => ("bash", $"-c \"{command}\""),
                    _ => ("cmd", $"/c {command}")
                };

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrWhiteSpace(workDir))
                    psi.WorkingDirectory = workDir;

                using var process = new Process { StartInfo = psi };
                process.Start();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

                await process.WaitForExitAsync(cts.Token);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                var output = stdout;
                if (!string.IsNullOrEmpty(stderr))
                    output += (string.IsNullOrEmpty(output) ? "" : "\n--- stderr ---\n") + stderr;

                bool truncated = false;
                if (output.Length > MaxOutputBytes)
                {
                    output = output[..MaxOutputBytes];
                    truncated = true;
                }

                return new ToolResult
                {
                    Success = process.ExitCode == 0,
                    Output = output,
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = truncated
                        ? $"Command exited with code {process.ExitCode} (output truncated to 100KB)"
                        : $"Command exited with code {process.ExitCode}",
                    ErrorMessage = process.ExitCode != 0 ? $"Exit code: {process.ExitCode}" : null
                };
            }
            catch (OperationCanceledException)
            {
                return new ToolResult { Success = false, ErrorMessage = "Command timed out." };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
