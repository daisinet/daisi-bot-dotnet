using DaisiBot.LocalTools.Clipboard;

namespace DaisiBot.LocalTools.Tests.Clipboard
{
    public class ClipboardWriteToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ClipboardWriteTool();
            Assert.Equal("daisi-clipboard-write", tool.Id);
        }

        [Fact]
        public void Parameters_TextIsRequired()
        {
            var tool = new ClipboardWriteTool();
            Assert.True(tool.Parameters.First(p => p.Name == "text").IsRequired);
        }
    }
}
