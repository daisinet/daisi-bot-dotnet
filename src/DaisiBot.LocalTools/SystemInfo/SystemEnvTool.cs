using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Text.Json;

namespace DaisiBot.LocalTools.SystemInfo
{
    public class SystemEnvTool : DaisiToolBase
    {
        private const string P_NAME = "name";
        private const string P_FILTER = "filter";

        public override string Id => "daisi-system-env";
        public override string Name => "Daisi System Environment";

        public override string UseInstructions =>
            "Use this tool to read environment variables. " +
            "Keywords: environment variable, env var, get env, PATH.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_NAME, Description = "Specific environment variable name to read.", IsRequired = false },
            new() { Name = P_FILTER, Description = "Filter variable names by partial match.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var name = parameters.GetParameter(P_NAME, false)?.Value;
            var filter = parameters.GetParameter(P_FILTER, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Reading environment variables",
                ExecutionTask = Task.Run(() => GetEnvVars(name, filter))
            };
        }

        private static ToolResult GetEnvVars(string? name, string? filter)
        {
            try
            {
                if (!string.IsNullOrEmpty(name))
                {
                    var value = Environment.GetEnvironmentVariable(name);
                    if (value == null)
                        return new ToolResult { Success = false, ErrorMessage = $"Environment variable '{name}' not found." };

                    var json = JsonSerializer.Serialize(new { name, value });
                    return new ToolResult
                    {
                        Success = true,
                        Output = json,
                        OutputFormat = InferenceOutputFormats.Json,
                        OutputMessage = $"Variable: {name}"
                    };
                }

                var vars = Environment.GetEnvironmentVariables();
                var dict = new Dictionary<string, string>();
                foreach (System.Collections.DictionaryEntry entry in vars)
                {
                    var key = entry.Key.ToString()!;
                    if (string.IsNullOrEmpty(filter) || key.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        dict[key] = entry.Value?.ToString() ?? "";
                }

                var sortedDict = dict.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
                var resultJson = JsonSerializer.Serialize(sortedDict, new JsonSerializerOptions { WriteIndented = true });
                return new ToolResult
                {
                    Success = true,
                    Output = resultJson,
                    OutputFormat = InferenceOutputFormats.Json,
                    OutputMessage = $"Found {sortedDict.Count} variables"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
