using DaisiBot.LocalTools.Screen;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Screen
{
    public class ScreenLocateToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ScreenLocateTool();
            Assert.Equal("daisi-screen-locate", tool.Id);
        }

        [Fact]
        public async Task Execute_ReturnsPlaceholderError()
        {
            var tool = new ScreenLocateTool();
            var context = new MockToolContext();

            var execContext = tool.GetExecutionContext(context, CancellationToken.None);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("not yet available", result.ErrorMessage);
        }
    }
}
