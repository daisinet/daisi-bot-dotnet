using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Drawing;
using System.Drawing.Imaging;

namespace DaisiBot.LocalTools.Screen
{
    public class ScreenCaptureTool : DaisiToolBase
    {
        private const string P_MODE = "mode";
        private const string P_X = "x";
        private const string P_Y = "y";
        private const string P_WIDTH = "width";
        private const string P_HEIGHT = "height";

        public override string Id => "daisi-screen-capture";
        public override string Name => "Daisi Screen Capture";

        public override string UseInstructions =>
            "Use this tool to capture a screenshot of the entire screen or a specific region. " +
            "Returns the image as a base64 encoded PNG. " +
            "Keywords: screenshot, screen capture, capture screen, print screen.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_MODE, Description = "Capture mode: full or region. Default is full.", IsRequired = false },
            new() { Name = P_X, Description = "X coordinate of the region (required for region mode).", IsRequired = false },
            new() { Name = P_Y, Description = "Y coordinate of the region (required for region mode).", IsRequired = false },
            new() { Name = P_WIDTH, Description = "Width of the region (required for region mode).", IsRequired = false },
            new() { Name = P_HEIGHT, Description = "Height of the region (required for region mode).", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var mode = parameters.GetParameterValueOrDefault(P_MODE, "full");

            return new ToolExecutionContext
            {
                ExecutionMessage = "Capturing screen",
                ExecutionTask = Task.Run(() => CaptureScreen(mode, parameters))
            };
        }

        private static ToolResult CaptureScreen(string mode, ToolParameterBase[] parameters)
        {
            try
            {
                int x = 0, y = 0;
                int width = NativeInterop.GetSystemMetrics(NativeInterop.SM_CXSCREEN);
                int height = NativeInterop.GetSystemMetrics(NativeInterop.SM_CYSCREEN);

                if (mode.ToLower() == "region")
                {
                    var xStr = parameters.GetParameter(P_X, false)?.Value;
                    var yStr = parameters.GetParameter(P_Y, false)?.Value;
                    var wStr = parameters.GetParameter(P_WIDTH, false)?.Value;
                    var hStr = parameters.GetParameter(P_HEIGHT, false)?.Value;

                    if (string.IsNullOrEmpty(xStr) || string.IsNullOrEmpty(yStr) ||
                        string.IsNullOrEmpty(wStr) || string.IsNullOrEmpty(hStr))
                    {
                        return new ToolResult { Success = false, ErrorMessage = "Region mode requires x, y, width, and height parameters." };
                    }

                    x = int.Parse(xStr);
                    y = int.Parse(yStr);
                    width = int.Parse(wStr);
                    height = int.Parse(hStr);
                }

                var desktopWnd = NativeInterop.GetDesktopWindow();
                var desktopDC = NativeInterop.GetWindowDC(desktopWnd);
                var memDC = NativeInterop.CreateCompatibleDC(desktopDC);
                var hBitmap = NativeInterop.CreateCompatibleBitmap(desktopDC, width, height);
                var oldBitmap = NativeInterop.SelectObject(memDC, hBitmap);

                NativeInterop.BitBlt(memDC, 0, 0, width, height, desktopDC, x, y, NativeInterop.SRCCOPY);

                NativeInterop.SelectObject(memDC, oldBitmap);

                using var bitmap = Image.FromHbitmap(hBitmap);
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                var base64 = Convert.ToBase64String(ms.ToArray());

                NativeInterop.DeleteObject(hBitmap);
                NativeInterop.DeleteDC(memDC);
                NativeInterop.ReleaseDC(desktopWnd, desktopDC);

                return new ToolResult
                {
                    Success = true,
                    Output = base64,
                    OutputFormat = InferenceOutputFormats.Base64,
                    OutputMessage = $"Screen captured ({width}x{height})"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
