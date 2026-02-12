using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.Shell
{
    public class ShellScriptTool : DaisiToolBase
    {
        private const string P_PATH = "path";
        private const string P_ARGUMENTS = "arguments";
        private const string P_WORKING_DIR = "working-directory";
        private const string P_TIMEOUT = "timeout";
        private const int MaxOutputBytes = 102_400;

        public override string Id => "daisi-shell-script";
        public override string Name => "Daisi Shell Script";

        public override string UseInstructions =>
            "Use this tool to execute a script file. Automatically detects the interpreter by file extension: " +
            ".ps1 → powershell, .bat/.cmd → cmd, .sh → bash, .py → python. " +
            "Keywords: run script, execute script, batch file, powershell script, bash script.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Full path to the script file.", IsRequired = true },
            new() { Name = P_ARGUMENTS, Description = "Arguments to pass to the script.", IsRequired = false },
            new() { Name = P_WORKING_DIR, Description = "Working directory for execution.", IsRequired = false },
            new() { Name = P_TIMEOUT, Description = "Timeout in seconds. Default is 60.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var arguments = parameters.GetParameterValueOrDefault(P_ARGUMENTS, "");
            var workDir = parameters.GetParameter(P_WORKING_DIR, false)?.Value;
            var timeoutStr = parameters.GetParameterValueOrDefault(P_TIMEOUT, "60");
            if (!int.TryParse(timeoutStr, out var timeoutSec)) timeoutSec = 60;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Running script: {path}",
                ExecutionTask = RunScript(path, arguments, workDir, timeoutSec, cancellation)
            };
        }

        private static async Task<ToolResult> RunScript(string path, string arguments, string? workDir, int timeoutSec, CancellationToken cancellation)
        {
            try
            {
                if (!File.Exists(path))
                    return new ToolResult { Success = false, ErrorMessage = $"Script file not found: {path}" };

                var ext = Path.GetExtension(path).ToLower();
                var (fileName, args) = ext switch
                {
                    ".ps1" => ("powershell", $"-NoProfile -File \"{path}\" {arguments}"),
                    ".bat" or ".cmd" => ("cmd", $"/c \"{path}\" {arguments}"),
                    ".sh" => ("bash", $"\"{path}\" {arguments}"),
                    ".py" => ("python", $"\"{path}\" {arguments}"),
                    _ => (path, arguments)
                };

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
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
                        ? $"Script exited with code {process.ExitCode} (output truncated)"
                        : $"Script exited with code {process.ExitCode}",
                    ErrorMessage = process.ExitCode != 0 ? $"Exit code: {process.ExitCode}" : null
                };
            }
            catch (OperationCanceledException)
            {
                return new ToolResult { Success = false, ErrorMessage = "Script execution timed out." };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
