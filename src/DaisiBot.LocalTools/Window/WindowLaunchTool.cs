using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.Window
{
    public class WindowLaunchTool : DaisiToolBase
    {
        private const string P_PATH = "path";
        private const string P_ARGUMENTS = "arguments";

        public override string Id => "daisi-window-launch";
        public override string Name => "Daisi Window Launch";

        public override string UseInstructions =>
            "Use this tool to launch an application. " +
            "Keywords: launch app, open application, start program, run application.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_PATH, Description = "Path to the executable or application to launch.", IsRequired = true },
            new() { Name = P_ARGUMENTS, Description = "Arguments to pass to the application.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var path = parameters.GetParameter(P_PATH).Value;
            var arguments = parameters.GetParameterValueOrDefault(P_ARGUMENTS, "");

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Launching: {path}",
                ExecutionTask = Task.Run(() => LaunchApp(path, arguments))
            };
        }

        private static ToolResult LaunchApp(string path, string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    UseShellExecute = true
                });

                return new ToolResult
                {
                    Success = true,
                    Output = $"Launched: {path} {arguments}".Trim(),
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Application launched"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
