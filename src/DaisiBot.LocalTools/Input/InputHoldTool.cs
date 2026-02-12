using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputHoldTool : DaisiToolBase
    {
        private const string P_KEY = "key";
        private const string P_ACTION = "action";

        public override string Id => "daisi-input-hold";
        public override string Name => "Daisi Input Hold";

        public override string UseInstructions =>
            "Use this tool to hold down or release a key. Useful for modifier keys or game inputs. " +
            "Keywords: hold key, release key, key down, key up.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_KEY, Description = "The key to hold or release (e.g. Shift, Ctrl, Alt).", IsRequired = true },
            new() { Name = P_ACTION, Description = "Action: hold or release.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var key = parameters.GetParameter(P_KEY).Value;
            var action = parameters.GetParameter(P_ACTION).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"{action} key: {key}",
                ExecutionTask = Task.Run(() => HoldOrRelease(key, action))
            };
        }

        private static ToolResult HoldOrRelease(string key, string action)
        {
            try
            {
                var vk = InputKeyTool.MapKeyName(key);
                if (vk == 0)
                    return new ToolResult { Success = false, ErrorMessage = $"Unknown key: {key}" };

                uint flags = action.ToLower() == "release" ? NativeInterop.KEYEVENTF_KEYUP : NativeInterop.KEYEVENTF_KEYDOWN;

                var inputs = new NativeInterop.INPUT[]
                {
                    new()
                    {
                        Type = NativeInterop.INPUT_KEYBOARD,
                        U = new() { ki = new() { wVk = vk, dwFlags = flags } }
                    }
                };
                NativeInterop.SendInput(1, inputs, Marshal.SizeOf<NativeInterop.INPUT>());

                return new ToolResult
                {
                    Success = true,
                    Output = $"Key {key} {action}",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Key {key} {action}"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
