using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputClickTool : DaisiToolBase
    {
        private const string P_X = "x";
        private const string P_Y = "y";
        private const string P_BUTTON = "button";
        private const string P_CLICKS = "clicks";

        public override string Id => "daisi-input-click";
        public override string Name => "Daisi Input Click";

        public override string UseInstructions =>
            "Use this tool to simulate a mouse click at a specific screen position. " +
            "Keywords: click, mouse click, left click, right click, double click.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_X, Description = "X coordinate to click.", IsRequired = true },
            new() { Name = P_Y, Description = "Y coordinate to click.", IsRequired = true },
            new() { Name = P_BUTTON, Description = "Mouse button: left, right, or middle. Default is left.", IsRequired = false },
            new() { Name = P_CLICKS, Description = "Number of clicks: 1 or 2. Default is 1.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var xStr = parameters.GetParameter(P_X).Value;
            var yStr = parameters.GetParameter(P_Y).Value;
            var button = parameters.GetParameterValueOrDefault(P_BUTTON, "left");
            var clicksStr = parameters.GetParameterValueOrDefault(P_CLICKS, "1");

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Clicking at ({xStr}, {yStr})",
                ExecutionTask = Task.Run(() => PerformClick(xStr, yStr, button, clicksStr))
            };
        }

        private static ToolResult PerformClick(string xStr, string yStr, string button, string clicksStr)
        {
            try
            {
                int x = int.Parse(xStr);
                int y = int.Parse(yStr);
                int clicks = int.TryParse(clicksStr, out var c) ? c : 1;

                NativeInterop.SetCursorPos(x, y);

                var (downFlag, upFlag) = button.ToLower() switch
                {
                    "right" => (NativeInterop.MOUSEEVENTF_RIGHTDOWN, NativeInterop.MOUSEEVENTF_RIGHTUP),
                    "middle" => (NativeInterop.MOUSEEVENTF_MIDDLEDOWN, NativeInterop.MOUSEEVENTF_MIDDLEUP),
                    _ => (NativeInterop.MOUSEEVENTF_LEFTDOWN, NativeInterop.MOUSEEVENTF_LEFTUP)
                };

                for (int i = 0; i < clicks; i++)
                {
                    var inputs = new NativeInterop.INPUT[]
                    {
                        new() { Type = NativeInterop.INPUT_MOUSE, U = new() { mi = new() { dwFlags = downFlag } } },
                        new() { Type = NativeInterop.INPUT_MOUSE, U = new() { mi = new() { dwFlags = upFlag } } }
                    };
                    NativeInterop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInterop.INPUT>());
                }

                return new ToolResult
                {
                    Success = true,
                    Output = $"Clicked {button} button {clicks} time(s) at ({x}, {y})",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Mouse {button} click at ({x}, {y})"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
