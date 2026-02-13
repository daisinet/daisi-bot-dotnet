using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.SystemInfo
{
    public class SystemProcessesToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemProcessesTool();
            Assert.Equal("daisi-system-processes", tool.Id);
        }

        [Fact]
        public async Task Execute_ReturnsNonEmptyArray()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemProcessesTool();
            var context = new MockToolContext();

            var execContext = tool.GetExecutionContext(context, CancellationToken.None);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.Contains("name", result.Output);
            Assert.Contains("pid", result.Output);
        }
    }
}
