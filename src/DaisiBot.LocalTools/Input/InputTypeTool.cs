using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputTypeTool : DaisiToolBase
    {
        private const string P_TEXT = "text";
        private const string P_DELAY = "delay";

        public override string Id => "daisi-input-type";
        public override string Name => "Daisi Input Type";

        public override string UseInstructions =>
            "Use this tool to type text using simulated keyboard input. " +
            "Keywords: type text, keyboard input, enter text, type string.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TEXT, Description = "The text to type.", IsRequired = true },
            new() { Name = P_DELAY, Description = "Delay in milliseconds between each character. Default is 0.", IsRequired = false }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var text = parameters.GetParameter(P_TEXT).Value;
            var delayStr = parameters.GetParameterValueOrDefault(P_DELAY, "0");

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Typing text ({text.Length} chars)",
                ExecutionTask = Task.Run(() => TypeText(text, delayStr))
            };
        }

        private static ToolResult TypeText(string text, string delayStr)
        {
            try
            {
                int delay = int.TryParse(delayStr, out var d) ? d : 0;

                foreach (char ch in text)
                {
                    var inputs = new NativeInterop.INPUT[]
                    {
                        new()
                        {
                            Type = NativeInterop.INPUT_KEYBOARD,
                            U = new() { ki = new() { wVk = 0, wScan = (ushort)ch, dwFlags = 0x0004 } } // KEYEVENTF_UNICODE
                        },
                        new()
                        {
                            Type = NativeInterop.INPUT_KEYBOARD,
                            U = new() { ki = new() { wVk = 0, wScan = (ushort)ch, dwFlags = 0x0004 | NativeInterop.KEYEVENTF_KEYUP } }
                        }
                    };
                    NativeInterop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInterop.INPUT>());

                    if (delay > 0)
                        Thread.Sleep(delay);
                }

                return new ToolResult
                {
                    Success = true,
                    Output = $"Typed {text.Length} character(s)",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Typed {text.Length} character(s)"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
