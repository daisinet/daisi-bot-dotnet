using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Text;

namespace DaisiBot.LocalTools.Window
{
    public class WindowResizeTool : DaisiToolBase
    {
        private const string P_TITLE = "title";
        private const string P_PID = "pid";
        private const string P_X = "x";
        private const string P_Y = "y";
        private const string P_WIDTH = "width";
        private const string P_HEIGHT = "height";

        public override string Id => "daisi-window-resize";
        public override string Name => "Daisi Window Resize";

        public override string UseInstructions =>
            "Use this tool to move and/or resize a window. Omitted position/size params keep the current values. " +
            "Keywords: resize window, move window, reposition window, change window size.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TITLE, Description = "Partial window title to match.", IsRequired = false },
            new() { Name = P_PID, Description = "Process ID of the window.", IsRequired = false },
            new() { Name = P_X, Description = "New X position.", IsRequired = false },
            new() { Name = P_Y, Description = "New Y position.", IsRequired = false },
            new() { Name = P_WIDTH, Description = "New width.", IsRequired = false },
            new() { Name = P_HEIGHT, Description = "New height.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var title = parameters.GetParameter(P_TITLE, false)?.Value;
            var pidStr = parameters.GetParameter(P_PID, false)?.Value;
            var xStr = parameters.GetParameter(P_X, false)?.Value;
            var yStr = parameters.GetParameter(P_Y, false)?.Value;
            var wStr = parameters.GetParameter(P_WIDTH, false)?.Value;
            var hStr = parameters.GetParameter(P_HEIGHT, false)?.Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Resizing window",
                ExecutionTask = Task.Run(() => ResizeWindow(title, pidStr, xStr, yStr, wStr, hStr))
            };
        }

        private static ToolResult ResizeWindow(string? title, string? pidStr, string? xStr, string? yStr, string? wStr, string? hStr)
        {
            try
            {
                var hWnd = FindWindow(title, pidStr);
                if (hWnd == IntPtr.Zero)
                    return new ToolResult { Success = false, ErrorMessage = "Window not found." };

                NativeInterop.GetWindowRect(hWnd, out var rect);

                int x = string.IsNullOrEmpty(xStr) ? rect.Left : int.Parse(xStr);
                int y = string.IsNullOrEmpty(yStr) ? rect.Top : int.Parse(yStr);
                int w = string.IsNullOrEmpty(wStr) ? rect.Right - rect.Left : int.Parse(wStr);
                int h = string.IsNullOrEmpty(hStr) ? rect.Bottom - rect.Top : int.Parse(hStr);

                NativeInterop.MoveWindow(hWnd, x, y, w, h, true);

                return new ToolResult
                {
                    Success = true,
                    Output = $"Window moved to ({x},{y}) size ({w}x{h})",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = "Window resized"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        internal static IntPtr FindWindow(string? title, string? pidStr)
        {
            uint? targetPid = null;
            if (!string.IsNullOrEmpty(pidStr) && uint.TryParse(pidStr, out var p))
                targetPid = p;

            IntPtr found = IntPtr.Zero;
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
                    if (sb.ToString().Contains(title, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }
    }
}
