using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Text;

namespace DaisiBot.LocalTools.Window
{
    public class WindowFocusTool : DaisiToolBase
    {
        private const string P_TITLE = "title";
        private const string P_PID = "pid";

        public override string Id => "daisi-window-focus";
        public override string Name => "Daisi Window Focus";

        public override string UseInstructions =>
            "Use this tool to bring a window to the foreground by title or PID. " +
            "Keywords: focus window, activate window, bring to front, switch to window.";

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
                ExecutionMessage = "Focusing window",
                ExecutionTask = Task.Run(() => FocusWindow(title, pidStr))
            };
        }

        private static ToolResult FocusWindow(string? title, string? pidStr)
        {
            try
            {
                uint? targetPid = null;
                if (!string.IsNullOrEmpty(pidStr) && uint.TryParse(pidStr, out var p))
                    targetPid = p;

                IntPtr found = IntPtr.Zero;
                string foundTitle = "";

                NativeInterop.EnumWindows((hWnd, lParam) =>
                {
                    if (!NativeInterop.IsWindowVisible(hWnd)) return true;

                    if (targetPid.HasValue)
                    {
                        NativeInterop.GetWindowThreadProcessId(hWnd, out uint wPid);
                        if (wPid == targetPid.Value) { found = hWnd; return false; }
                    }
                    else if (!string.IsNullOrEmpty(title))
                    {
                        var sb = new StringBuilder(256);
                        NativeInterop.GetWindowText(hWnd, sb, 256);
                        var wTitle = sb.ToString();
                        if (wTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
                        {
                            found = hWnd;
                            foundTitle = wTitle;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (found == IntPtr.Zero)
                    return new ToolResult { Success = false, ErrorMessage = "Window not found." };

                NativeInterop.ShowWindow(found, NativeInterop.SW_RESTORE);
                NativeInterop.SetForegroundWindow(found);

                return new ToolResult
                {
                    Success = true,
                    Output = $"Focused window: {foundTitle}",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Window focused"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
