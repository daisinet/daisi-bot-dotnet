using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Clipboard
{
    public class ClipboardWriteTool : DaisiToolBase
    {
        private const string P_TEXT = "text";

        public override string Id => "daisi-clipboard-write";
        public override string Name => "Daisi Clipboard Write";

        public override string UseInstructions =>
            "Use this tool to write text to the system clipboard. " +
            "Keywords: copy, clipboard, set clipboard, copy to clipboard.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TEXT, Description = "The text to write to the clipboard.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var text = parameters.GetParameter(P_TEXT).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = "Writing to clipboard",
                ExecutionTask = Task.Run(() => WriteClipboard(text))
            };
        }

        private static ToolResult WriteClipboard(string text)
        {
            try
            {
                if (!NativeInterop.OpenClipboard(IntPtr.Zero))
                    return new ToolResult { Success = false, ErrorMessage = "Failed to open clipboard." };

                try
                {
                    NativeInterop.EmptyClipboard();

                    var bytes = (text.Length + 1) * 2;
                    var hMem = NativeInterop.GlobalAlloc(NativeInterop.GMEM_MOVEABLE, (UIntPtr)bytes);
                    if (hMem == IntPtr.Zero)
                        return new ToolResult { Success = false, ErrorMessage = "Failed to allocate memory." };

                    var ptr = NativeInterop.GlobalLock(hMem);
                    Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                    Marshal.WriteInt16(ptr + text.Length * 2, 0);
                    NativeInterop.GlobalUnlock(hMem);

                    NativeInterop.SetClipboardData(NativeInterop.CF_UNICODETEXT, hMem);

                    return new ToolResult
                    {
                        Success = true,
                        Output = $"Copied {text.Length} character(s) to clipboard",
                        OutputFormat = InferenceOutputFormats.PlainText,
                        OutputMessage = $"Text copied to clipboard ({text.Length} chars)"
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
