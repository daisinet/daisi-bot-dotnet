using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Native;
using System.Runtime.InteropServices;

namespace DaisiBot.LocalTools.Input
{
    public class InputKeyTool : DaisiToolBase
    {
        private const string P_KEY = "key";

        public override string Id => "daisi-input-key";
        public override string Name => "Daisi Input Key";

        public override string UseInstructions =>
            "Use this tool to press a key or key combination. Supports modifier keys like Ctrl, Alt, Shift, Win. " +
            "Examples: Enter, Ctrl+C, Alt+Tab, Ctrl+Shift+S. " +
            "Keywords: press key, key combo, keyboard shortcut, hotkey.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_KEY, Description = "Key or key combination to press (e.g. Enter, Ctrl+C, Alt+Tab).", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            var key = parameters.GetParameter(P_KEY).Value;

            return new ToolExecutionContext
            {
                ExecutionMessage = $"Pressing key: {key}",
                ExecutionTask = Task.Run(() => PressKey(key))
            };
        }

        private static ToolResult PressKey(string keyCombo)
        {
            try
            {
                var parts = keyCombo.Split('+').Select(p => p.Trim()).ToArray();
                var vkCodes = new List<ushort>();

                foreach (var part in parts)
                {
                    var vk = MapKeyName(part);
                    if (vk == 0)
                        return new ToolResult { Success = false, ErrorMessage = $"Unknown key: {part}" };
                    vkCodes.Add(vk);
                }

                var downInputs = vkCodes.Select(vk => new NativeInterop.INPUT
                {
                    Type = NativeInterop.INPUT_KEYBOARD,
                    U = new() { ki = new() { wVk = vk, dwFlags = NativeInterop.KEYEVENTF_KEYDOWN } }
                }).ToArray();

                var upInputs = vkCodes.AsEnumerable().Reverse().Select(vk => new NativeInterop.INPUT
                {
                    Type = NativeInterop.INPUT_KEYBOARD,
                    U = new() { ki = new() { wVk = vk, dwFlags = NativeInterop.KEYEVENTF_KEYUP } }
                }).ToArray();

                var allInputs = downInputs.Concat(upInputs).ToArray();
                NativeInterop.SendInput((uint)allInputs.Length, allInputs, Marshal.SizeOf<NativeInterop.INPUT>());

                return new ToolResult
                {
                    Success = true,
                    Output = $"Pressed: {keyCombo}",
                    OutputFormat = InferenceOutputFormats.PlainText,
                    OutputMessage = $"Key press: {keyCombo}"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        internal static ushort MapKeyName(string name) => name.ToLower() switch
        {
            "ctrl" or "control" => 0x11,
            "alt" => 0x12,
            "shift" => 0x10,
            "win" or "windows" => 0x5B,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "escape" or "esc" => 0x1B,
            "space" => 0x20,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "insert" or "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" or "pgup" => 0x21,
            "pagedown" or "pgdn" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
            "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
            "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
            "printscreen" or "prtsc" => 0x2C,
            "capslock" => 0x14,
            "numlock" => 0x90,
            "scrolllock" => 0x91,
            _ when name.Length == 1 && char.IsLetterOrDigit(name[0]) => (ushort)char.ToUpper(name[0]),
            _ => 0
        };
    }
}
