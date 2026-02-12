using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Browser;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Browser
{
    public class BrowserScreenshotToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new BrowserScreenshotTool();
            Assert.Equal("daisi-browser-screenshot", tool.Id);
        }

        [Fact]
        public async Task Execute_ReturnsPlaceholderError()
        {
            var tool = new BrowserScreenshotTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "url", Value = "https://example.com", IsRequired = true }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("not yet available", result.ErrorMessage);
        }
    }
}
