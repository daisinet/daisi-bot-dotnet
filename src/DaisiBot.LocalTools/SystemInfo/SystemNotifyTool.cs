using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;

namespace DaisiBot.LocalTools.SystemInfo
{
    public class SystemNotifyTool : DaisiToolBase
    {
        private const string P_TITLE = "title";
        private const string P_MESSAGE = "message";

        public override string Id => "daisi-system-notify";
        public override string Name => "Daisi System Notify";

        public override string UseInstructions =>
            "Use this tool to show a desktop notification. " +
            "Keywords: notification, toast, alert, notify, desktop notification.";

        public override ToolParameter[] Parameters => [
            new() { Name = P_TITLE, Description = "Notification title.", IsRequired = true },
            new() { Name = P_MESSAGE, Description = "Notification message.", IsRequired = true }
        ];

        public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
        {
            return new ToolExecutionContext
            {
                ExecutionMessage = "Sending notification",
                ExecutionTask = Task.FromResult(new ToolResult
                {
                    Success = false,
                    ErrorMessage = "Desktop notifications are not yet available. This feature is planned for a future release."
                })
            };
        }
    }
}
