using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;

namespace DaisiBot.LocalTools.Input
{
    public class InputMoveTool : DaisiToolBase
    {
        private const string P_X = "x";
        private const string P_Y = "y";

        public override string Id => "daisi-input-move";
        public override string Name => "Daisi Input Move";

        public override string UseInstructions =>
            "Use this tool to move the mouse cursor to a specific screen position. " +
            "Keywords: move mouse, cursor, position cursor.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_X, Description = "X coordinate to move to.", IsRequired = true },
            new() { Name = P_Y, Description = "Y coordinate to move to.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var xStr = parameters.GetParameter(P_X).Value;
            var yStr = parameters.GetParameter(P_Y).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Moving cursor to ({xStr}, {yStr})",
                ExecutionTask = Task.Run(() =>
                {
                    try
                    {
                        int x = int.Parse(xStr);
                        int y = int.Parse(yStr);
                        NativeInterop.SetCursorPos(x, y);
                        return new ToolResult
                        {
                            Success = true,
                            Output = $"Cursor moved to ({x}, {y})",
                            OutputFormat = InferenceOutputFormats.PlainText,
                            OutputMessage = $"Cursor moved to ({x}, {y})"
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ToolResult { Success = false, ErrorMessage = ex.Message };
                    }
                })
            };
        }
    }
}
