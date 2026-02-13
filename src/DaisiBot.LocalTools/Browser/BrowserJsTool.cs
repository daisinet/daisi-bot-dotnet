using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserJsTool : DaisiToolBase
    {
        private const string P_URL = "url";
        private const string P_SCRIPT = "script";

        public override string Id => "daisi-browser-js";
        public override string Name => "Daisi Browser JavaScript";

        public override string UseInstructions =>
            "Use this tool to execute JavaScript on a web page. " +
            "Keywords: run javascript, execute script, browser script, js.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_URL, Description = "The URL of the page to run the script on.", IsRequired = true },
            new() { Name = P_SCRIPT, Description = "The JavaScript code to execute.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Executing browser JavaScript",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Browser JavaScript execution is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
