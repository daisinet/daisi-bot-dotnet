using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserClickTool : DaisiToolBase
    {
        private const string P_SELECTOR = "selector";

        public override string Id => "daisi-browser-click";
        public override string Name => "Daisi Browser Click";

        public override string UseInstructions =>
            "Use this tool to click an element on a web page by CSS selector. " +
            "Keywords: click element, click button, web automation, click link.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_SELECTOR, Description = "CSS selector of the element to click.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Clicking browser element",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Browser click is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
