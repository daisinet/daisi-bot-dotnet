using DaisiBot.LocalTools.Clipboard;

namespace DaisiBot.LocalTools.Tests.Clipboard
{
    public class ClipboardReadToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ClipboardReadTool();
            Assert.Equal("daisi-clipboard-read", tool.Id);
        }

        [Fact]
        public void Parameters_IsEmpty()
        {
            var tool = new ClipboardReadTool();
            Assert.Empty(tool.Parameters);
        }
    }
}
