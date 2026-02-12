using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Drawing;
using System.Drawing.Imaging;

namespace DaisiBot.LocalTools.Clipboard
{
    public class ClipboardReadImageTool : DaisiToolBase
    {
        public override string Id => "daisi-clipboard-read-image";
        public override string Name => "Daisi Clipboard Read Image";

        public override string UseInstructions =>
            "Use this tool to read an image from the system clipboard and return it as base64 PNG. " +
            "Keywords: paste image, clipboard image, get image from clipboard.";

        public override ToolParameter[] Parameters => [];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Reading image from clipboard",
                ExecutionTask = Task.Run(ReadClipboardImage)
            };
        }

        private static ToolResult ReadClipboardImage()
        {
            try
            {
                if (!NativeInterop.IsClipboardFormatAvailable(NativeInterop.CF_BITMAP) &&
                    !NativeInterop.IsClipboardFormatAvailable(NativeInterop.CF_DIB))
                {
                    return new ToolResult { Success = false, ErrorMessage = "No image data in clipboard." };
                }

                if (!NativeInterop.OpenClipboard(IntPtr.Zero))
                    return new ToolResult { Success = false, ErrorMessage = "Failed to open clipboard." };

                try
                {
                    var hBitmap = NativeInterop.GetClipboardData(NativeInterop.CF_BITMAP);
                    if (hBitmap == IntPtr.Zero)
                        return new ToolResult { Success = false, ErrorMessage = "Failed to get bitmap from clipboard." };

                    using var bitmap = Image.FromHbitmap(hBitmap);
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Png);
                    var base64 = Convert.ToBase64String(ms.ToArray());

                    return new ToolResult
                    {
                        Success = true,
                        Output = base64,
                        OutputFormat = InferenceOutputFormats.Base64,
                        OutputMessage = $"Clipboard image ({bitmap.Width}x{bitmap.Height})"
                    };
                }
                finally
                {
                    NativeInterop.CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
