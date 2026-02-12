using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.SystemInfo
{
    public class SystemKillTool : DaisiToolBase
    {
        private const string P_PID = "pid";
        private const string P_NAME = "name";

        public override string Id => "daisi-system-kill";
        public override string Name => "Daisi System Kill";

        public override string UseInstructions =>
            "Use this tool to kill a process by PID or name. " +
            "Keywords: kill process, end task, terminate process, stop process.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PID, Description = "Process ID to kill.", IsRequired = false },
            new() { Name = P_NAME, Description = "Process name to kill.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var pidStr = parameters.GetParameter(P_PID, false)?.Value;
            var name = parameters.GetParameter(P_NAME, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Killing process",
                ExecutionTask = Task.Run(() => KillProcess(pidStr, name))
            };
        }

        private static ToolResult KillProcess(string? pidStr, string? name)
        {
            try
            {
                if (string.IsNullOrEmpty(pidStr) && string.IsNullOrEmpty(name))
                    return new ToolResult { Success = false, ErrorMessage = "Either pid or name must be provided." };

                if (!string.IsNullOrEmpty(pidStr) && int.TryParse(pidStr, out var pid))
                {
                    var process = Process.GetProcessById(pid);
                    var pName = process.ProcessName;
                    process.Kill();
                    return new ToolResult
                    {
                        Success = true,
                        Output = $"Killed process {pName} (PID: {pid})",
                        OutputFormat = InferenceOutputFormats.PlainText,
                        OutputMessage = "Process killed"
                    };
                }

                if (!string.IsNullOrEmpty(name))
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length == 0)
                        return new ToolResult { Success = false, ErrorMessage = $"No process found with name: {name}" };

                    foreach (var p in processes) p.Kill();

                    return new ToolResult
                    {
                        Success = true,
                        Output = $"Killed {processes.Length} process(es) named '{name}'",
                        OutputFormat = InferenceOutputFormats.PlainText,
                        OutputMessage = "Process(es) killed"
                    };
                }

                return new ToolResult { Success = false, ErrorMessage = "Invalid pid value." };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
