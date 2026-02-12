using DaisiBot.LocalTools.Screen;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Screen
{
    public class ScreenOcrToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ScreenOcrTool();
            Assert.Equal("daisi-screen-ocr", tool.Id);
        }

        [Fact]
        public async Task Execute_ReturnsPlaceholderError()
        {
            var tool = new ScreenOcrTool();
            var context = new MockToolContext();

            var execContext = tool.GetExecutionContext(context, CancellationToken.None);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("not yet available", result.ErrorMessage);
        }
    }
}
