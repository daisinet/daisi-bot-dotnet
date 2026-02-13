using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.Screen
{
    public class ScreenOcrTool : DaisiToolBase
    {
        private const string P_X = "x";
        private const string P_Y = "y";
        private const string P_WIDTH = "width";
        private const string P_HEIGHT = "height";

        public override string Id => "daisi-screen-ocr";
        public override string Name => "Daisi Screen OCR";

        public override string UseInstructions =>
            "Use this tool to perform OCR (optical character recognition) on a screen region. " +
            "Keywords: read screen text, ocr, extract text from screen.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_X, Description = "X coordinate of the region.", IsRequired = false },
            new() { Name = P_Y, Description = "Y coordinate of the region.", IsRequired = false },
            new() { Name = P_WIDTH, Description = "Width of the region.", IsRequired = false },
            new() { Name = P_HEIGHT, Description = "Height of the region.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Performing screen OCR",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Screen OCR is not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
