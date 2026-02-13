using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.SystemInfo
{
    public class SystemKillToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemKillTool();
            Assert.Equal("daisi-system-kill", tool.Id);
        }

        [Fact]
        public async Task Execute_NoPidOrName_ReturnsError()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemKillTool();
            var context = new MockToolContext();

            var execContext = tool.GetExecutionContext(context, CancellationToken.None);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("pid or name", result.ErrorMessage);
        }
    }
}
