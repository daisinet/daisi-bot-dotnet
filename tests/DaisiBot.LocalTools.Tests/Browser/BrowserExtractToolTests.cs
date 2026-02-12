using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Browser;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Browser
{
    public class BrowserExtractToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new BrowserExtractTool();
            Assert.Equal("daisi-browser-extract", tool.Id);
        }

        [Fact]
        public async Task Execute_NoHttpClientFactory_ReturnsError()
        {
            var tool = new BrowserExtractTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "url", Value = "https://example.com", IsRequired = true }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("HttpClientFactory", result.ErrorMessage);
        }
    }
}
