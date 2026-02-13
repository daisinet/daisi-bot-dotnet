using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using System.Diagnostics;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserOpenTool : DaisiToolBase
    {
        private const string P_URL = "url";

        public override string Id => "daisi-browser-open";
        public override string Name => "Daisi Browser Open";

        public override string UseInstructions =>
            "Use this tool to open a URL in the default web browser. " +
            "Keywords: open url, open website, browse, launch browser, open link.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_URL, Description = "The URL to open in the browser.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var url = parameters.GetParameter(P_URL).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Opening: {url}",
                ExecutionTask = Task.Run(() => OpenBrowser(url))
            };
        }

        private static ToolResult OpenBrowser(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    return new ToolResult { Success = false, ErrorMessage = "Invalid URL. Must be an http or https URL." };
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                return new ToolResult
                {
                    Success = true,
                    Output = $"Opened {url} in default browser",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Opened URL in browser"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
