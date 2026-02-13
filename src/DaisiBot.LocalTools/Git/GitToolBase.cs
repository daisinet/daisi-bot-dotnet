using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.Git
{
    public abstract class GitToolBase : DaisiToolBase
    {
        protected static async Task<(bool success, string output)> RunGitAsync(string arguments, string workingDirectory, CancellationToken cancellation = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellation);
            var stderr = await process.StandardError.ReadToEndAsync(cancellation);

            await process.WaitForExitAsync(cancellation);

            var output = stdout;
            if (!string.IsNullOrEmpty(stderr) && process.ExitCode != 0)
                output += (string.IsNullOrEmpty(output) ? "" : "\n") + stderr;

            return (process.ExitCode == 0, output);
        }
    }
}
