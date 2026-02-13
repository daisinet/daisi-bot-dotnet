using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Browser
{
    public class BrowserTypeTool : DaisiToolBase
    {
        private const string P_SELECTOR = "selector";
        private const string P_TEXT = "text";

        public override string Id => "daisi-browser-type";
        public override string Name => "Daisi Browser Type";

        public override string UseInstructions =>
            "Use this tool to type text into a web page element by CSS selector. " +
            "Keywords: fill form, type in field, web input, enter text in browser.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_SELECTOR, Description = "CSS selector of the input element.", IsRequired = true },
            new() { Name = P_TEXT, Description = "Text to type into the element.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Typing in browser element",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Browser type is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
