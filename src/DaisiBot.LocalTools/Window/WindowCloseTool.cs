using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;

namespace DaisiBot.LocalTools.Window
{
    public class WindowCloseTool : DaisiToolBase
    {
        private const string P_TITLE = "title";
        private const string P_PID = "pid";

        public override string Id => "daisi-window-close";
        public override string Name => "Daisi Window Close";

        public override string UseInstructions =>
            "Use this tool to close a window by sending WM_CLOSE. " +
            "Keywords: close window, close app, close application.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TITLE, Description = "Partial window title to match.", IsRequired = false },
            new() { Name = P_PID, Description = "Process ID of the window.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var title = parameters.GetParameter(P_TITLE, false)?.Value;
            var pidStr = parameters.GetParameter(P_PID, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Closing window",
                ExecutionTask = Task.Run(() => CloseWindow(title, pidStr))
            };
        }

        private static ToolResult CloseWindow(string? title, string? pidStr)
        {
            try
            {
                var hWnd = WindowResizeTool.FindWindow(title, pidStr);
                if (hWnd == IntPtr.Zero)
                    return new ToolResult { Success = false, ErrorMessage = "Window not found." };

                NativeInterop.PostMessage(hWnd, NativeInterop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                return new ToolResult
                {
                    Success = true,
                    Output = "Window close message sent",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Window closed"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
