using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputScrollTool : DaisiToolBase
    {
        private const string P_AMOUNT = "amount";
        private const string P_X = "x";
        private const string P_Y = "y";

        public override string Id => "daisi-input-scroll";
        public override string Name => "Daisi Input Scroll";

        public override string UseInstructions =>
            "Use this tool to simulate mouse wheel scrolling. Positive values scroll up, negative values scroll down. " +
            "Keywords: scroll, mouse wheel, scroll up, scroll down.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_AMOUNT, Description = "Scroll amount. Positive = up, negative = down. Each unit is 120 (one wheel notch).", IsRequired = true },
            new() { Name = P_X, Description = "X coordinate to scroll at. If omitted, scrolls at current cursor position.", IsRequired = false },
            new() { Name = P_Y, Description = "Y coordinate to scroll at. If omitted, scrolls at current cursor position.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var amountStr = parameters.GetParameter(P_AMOUNT).Value;
            var xStr = parameters.GetParameter(P_X, false)?.Value;
            var yStr = parameters.GetParameter(P_Y, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Scrolling {amountStr}",
                ExecutionTask = Task.Run(() => PerformScroll(amountStr, xStr, yStr))
            };
        }

        private static ToolResult PerformScroll(string amountStr, string? xStr, string? yStr)
        {
            try
            {
                int amount = int.Parse(amountStr);

                if (!string.IsNullOrEmpty(xStr) && !string.IsNullOrEmpty(yStr))
                    NativeInterop.SetCursorPos(int.Parse(xStr), int.Parse(yStr));

                var inputs = new NativeInterop.INPUT[]
                {
                    new()
                    {
                        Type = NativeInterop.INPUT_MOUSE,
                        U = new() { mi = new() { dwFlags = NativeInterop.MOUSEEVENTF_WHEEL, mouseData = amount * 120 } }
                    }
                };
                NativeInterop.SendInput(1, inputs, Marshal.SizeOf<NativeInterop.INPUT>());

                var direction = amount > 0 ? "up" : "down";
                var absAmount = amount < 0 ? -amount : amount;
                return new ToolResult
                {
                    Success = true,
                    Output = $"Scrolled {direction} by {absAmount} notch(es)",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Scrolled {direction}"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
