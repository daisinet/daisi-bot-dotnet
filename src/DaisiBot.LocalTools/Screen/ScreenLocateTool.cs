using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Screen
{
    public class ScreenLocateTool : DaisiToolBase
    {
        private const string P_TEXT = "text";
        private const string P_IMAGE_BASE64 = "image-base64";

        public override string Id => "daisi-screen-locate";
        public override string Name => "Daisi Screen Locate";

        public override string UseInstructions =>
            "Use this tool to locate text or an image on the screen. " +
            "Keywords: find on screen, locate element, screen search, find text on screen.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TEXT, Description = "Text to locate on the screen.", IsRequired = false },
            new() { Name = P_IMAGE_BASE64, Description = "Base64-encoded image to locate on the screen.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Locating element on screen",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Screen locate is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
