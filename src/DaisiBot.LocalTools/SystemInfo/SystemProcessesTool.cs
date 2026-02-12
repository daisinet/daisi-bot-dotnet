using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;
using System.Text.Json;

namespace DaisiBot.LocalTools.SystemInfo
{
    public class SystemProcessesTool : DaisiToolBase
    {
        private const string P_FILTER = "filter";
        private const string P_MAX_RESULTS = "max-results";
        private const string P_SORT_BY = "sort-by";

        public override string Id => "daisi-system-processes";
        public override string Name => "Daisi System Processes";

        public override string UseInstructions =>
            "Use this tool to list running processes. Can filter by name and sort by name or memory. " +
            "Keywords: list processes, running processes, task manager, process list.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_FILTER, Description = "Filter processes by name (partial match).", IsRequired = false },
            new() { Name = P_MAX_RESULTS, Description = "Maximum number of results. Default is 50.", IsRequired = false },
            new() { Name = P_SORT_BY, Description = "Sort by: name or memory. Default is name.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var filter = parameters.GetParameter(P_FILTER, false)?.Value;
            var maxStr = parameters.GetParameterValueOrDefault(P_MAX_RESULTS, "50");
            var sortBy = parameters.GetParameterValueOrDefault(P_SORT_BY, "name");
            if (!int.TryParse(maxStr, out var max)) max = 50;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Listing processes",
                ExecutionTask = Task.Run(() => ListProcesses(filter, max, sortBy))
            };
        }

        private static ToolResult ListProcesses(string? filter, int max, string sortBy)
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Select(p =>
                    {
                        try
                        {
                            return new { name = p.ProcessName, pid = p.Id, memoryMB = p.WorkingSet64 / (1024 * 1024) };
                        }
                        catch { return null; }
                    })
                    .Where(p => p != null)
                    .Where(p => string.IsNullOrEmpty(filter) || p!.name.Contains(filter, StringComparison.OrdinalIgnoreCase));

                var sorted = sortBy.ToLower() == "memory"
                    ? processes.OrderByDescending(p => p!.memoryMB)
                    : processes.OrderBy(p => p!.name);

                var result = sorted.Take(max).ToArray();

                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                return new ToolResult
                {
                    Success = true,
                    Output = json,
                    OutputFormat = InferenceOutputFormats.Json,
                    OutputMessage = $"Listed {result.Length} processes"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
