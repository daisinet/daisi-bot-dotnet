using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DaisiBot.LocalTools.Window
{
    public class WindowListTool : DaisiToolBase
    {
        public override string Id => "daisi-window-list";
        public override string Name => "Daisi Window List";

        public override string UseInstructions =>
            "Use this tool to list all visible windows on the desktop. " +
            "Returns a JSON array with title, process name, PID, and position/size for each window. " +
            "Keywords: list windows, open windows, running windows, desktop windows.";

        public override ToolParameter[] Parameters => [];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Listing windows",
                ExecutionTask = Task.Run(ListWindows)
            };
        }

        private static ToolResult ListWindows()
        {
            try
            {
                var windows = new List<object>();
                NativeInterop.EnumWindows((hWnd, lParam) =>
                {
                    if (!NativeInterop.IsWindowVisible(hWnd)) return true;

                    var sb = new StringBuilder(256);
                    NativeInterop.GetWindowText(hWnd, sb, 256);
                    var title = sb.ToString();
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    NativeInterop.GetWindowThreadProcessId(hWnd, out uint pid);
                    string processName = "";
                    try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { }

                    NativeInterop.GetWindowRect(hWnd, out var rect);

                    windows.Add(new
                    {
                        title,
                        processName,
                        pid,
                        x = rect.Left,
                        y = rect.Top,
                        width = rect.Right - rect.Left,
                        height = rect.Bottom - rect.Top
                    });
                    return true;
                }, IntPtr.Zero);

                var json = JsonSerializer.Serialize(windows, new JsonSerializerOptions { WriteIndented = true });
                return new ToolResult
                {
                    Success = true,
                    Output = json,
                    OutputFormat = InferenceOutputFormats.Json,
                    OutputMessage = $"Found {windows.Count} windows"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
