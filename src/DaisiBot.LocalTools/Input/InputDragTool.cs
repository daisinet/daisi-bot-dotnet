using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputDragTool : DaisiToolBase
    {
        private const string P_START_X = "start-x";
        private const string P_START_Y = "start-y";
        private const string P_END_X = "end-x";
        private const string P_END_Y = "end-y";
        private const string P_BUTTON = "button";

        public override string Id => "daisi-input-drag";
        public override string Name => "Daisi Input Drag";

        public override string UseInstructions =>
            "Use this tool to simulate a mouse drag from one position to another. " +
            "Keywords: drag, mouse drag, drag and drop.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_START_X, Description = "Starting X coordinate.", IsRequired = true },
            new() { Name = P_START_Y, Description = "Starting Y coordinate.", IsRequired = true },
            new() { Name = P_END_X, Description = "Ending X coordinate.", IsRequired = true },
            new() { Name = P_END_Y, Description = "Ending Y coordinate.", IsRequired = true },
            new() { Name = P_BUTTON, Description = "Mouse button: left, right, or middle. Default is left.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var startX = parameters.GetParameter(P_START_X).Value;
            var startY = parameters.GetParameter(P_START_Y).Value;
            var endX = parameters.GetParameter(P_END_X).Value;
            var endY = parameters.GetParameter(P_END_Y).Value;
            var button = parameters.GetParameterValueOrDefault(P_BUTTON, "left");

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Dragging from ({startX},{startY}) to ({endX},{endY})",
                ExecutionTask = Task.Run(() => PerformDrag(startX, startY, endX, endY, button))
            };
        }

        private static ToolResult PerformDrag(string startXStr, string startYStr, string endXStr, string endYStr, string button)
        {
            try
            {
                int sx = int.Parse(startXStr), sy = int.Parse(startYStr);
                int ex = int.Parse(endXStr), ey = int.Parse(endYStr);

                var (downFlag, upFlag) = NativeInterop.GetMouseButtonFlags(button);

                NativeInterop.SetCursorPos(sx, sy);
                Thread.Sleep(50);

                var downInput = new NativeInterop.INPUT[]
                {
                    new() { Type = NativeInterop.INPUT_MOUSE, U = new() { mi = new() { dwFlags = downFlag } } }
                };
                NativeInterop.SendInput(1, downInput, Marshal.SizeOf<NativeInterop.INPUT>());

                Thread.Sleep(50);
                NativeInterop.SetCursorPos(ex, ey);
                Thread.Sleep(50);

                var upInput = new NativeInterop.INPUT[]
                {
                    new() { Type = NativeInterop.INPUT_MOUSE, U = new() { mi = new() { dwFlags = upFlag } } }
                };
                NativeInterop.SendInput(1, upInput, Marshal.SizeOf<NativeInterop.INPUT>());

                return new ToolResult
                {
                    Success = true,
                    Output = $"Dragged from ({sx},{sy}) to ({ex},{ey}) with {button} button",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Mouse drag completed"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
