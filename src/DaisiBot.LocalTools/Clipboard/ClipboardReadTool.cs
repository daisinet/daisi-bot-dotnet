using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Clipboard
{
    public class ClipboardReadTool : DaisiToolBase
    {
        public override string Id => "daisi-clipboard-read";
        public override string Name => "Daisi Clipboard Read";

        public override string UseInstructions =>
            "Use this tool to read text content from the system clipboard. " +
            "Keywords: paste, clipboard, read clipboard, get clipboard.";

        public override ToolParameter[] Parameters => [];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Reading clipboard",
                ExecutionTask = Task.Run(ReadClipboard)
            };
        }

        private static ToolResult ReadClipboard()
        {
            try
            {
                if (!NativeInterop.IsClipboardFormatAvailable(NativeInterop.CF_UNICODETEXT))
                    return new ToolResult { Success = true, Output = "", OutputMessage = "Clipboard is empty or does not contain text." };

                if (!NativeInterop.OpenClipboard(IntPtr.Zero))
                    return new ToolResult { Success = false, ErrorMessage = "Failed to open clipboard." };

                try
                {
                    var hData = NativeInterop.GetClipboardData(NativeInterop.CF_UNICODETEXT);
                    if (hData == IntPtr.Zero)
                        return new ToolResult { Success = true, Output = "", OutputMessage = "No text data in clipboard." };

                    var ptr = NativeInterop.GlobalLock(hData);
                    if (ptr == IntPtr.Zero)
                        return new ToolResult { Success = false, ErrorMessage = "Failed to lock clipboard data." };

                    try
                    {
                        var text = Marshal.PtrToStringUni(ptr) ?? "";
                        return new ToolResult
                        {
                            Success = true,
                            Output = text,
                            OutputFormat = InferenceOutputFormats.PlainText,
                            OutputMessage = $"Clipboard text ({text.Length} chars)"
                        };
                    }
                    finally
                    {
                        NativeInterop.GlobalUnlock(hData);
                    }
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
