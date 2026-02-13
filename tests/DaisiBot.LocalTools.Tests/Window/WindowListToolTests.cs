using DaisiBot.LocalTools.Window;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Window
{
    public class WindowListToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new WindowListTool();
            Assert.Equal("daisi-window-list", tool.Id);
        }

        [Fact]
        public void Parameters_IsEmpty()
        {
            var tool = new WindowListTool();
            Assert.Empty(tool.Parameters);
        }

        [Fact]
        [Trait("Category", "Desktop")]
        public async Task Execute_ReturnsNonEmptyList()
        {
            var tool = new WindowListTool();
            var context = new MockToolContext();

            var execContext = tool.GetExecutionContext(context, CancellationToken.None);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Output));
            Assert.Contains("title", result.Output);
        }
    }
}
