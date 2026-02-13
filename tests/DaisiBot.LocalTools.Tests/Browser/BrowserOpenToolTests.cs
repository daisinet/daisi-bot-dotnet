using DaisiBot.LocalTools.Browser;

namespace DaisiBot.LocalTools.Tests.Browser
{
    public class BrowserOpenToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new BrowserOpenTool();
            Assert.Equal("daisi-browser-open", tool.Id);
        }

        [Fact]
        public void Parameters_UrlIsRequired()
        {
            var tool = new BrowserOpenTool();
            Assert.True(tool.Parameters.First(p => p.Name == "url").IsRequired);
        }
    }
}
