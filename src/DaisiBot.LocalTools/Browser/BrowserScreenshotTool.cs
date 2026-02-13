using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserScreenshotTool : DaisiToolBase
    {
        private const string P_URL = "url";

        public override string Id => "daisi-browser-screenshot";
        public override string Name => "Daisi Browser Screenshot";

        public override string UseInstructions =>
            "Use this tool to take a screenshot of a web page. " +
            "Keywords: web screenshot, page screenshot, capture website.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_URL, Description = "The URL to screenshot.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Taking browser screenshot",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Browser screenshot is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
