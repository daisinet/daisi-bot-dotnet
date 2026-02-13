using DaisiBot.LocalTools.Window;

namespace DaisiBot.LocalTools.Tests.Window
{
    public class WindowLaunchToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new WindowLaunchTool();
            Assert.Equal("daisi-window-launch", tool.Id);
        }

        [Fact]
        public void Parameters_PathIsRequired()
        {
            var tool = new WindowLaunchTool();
            Assert.True(tool.Parameters.First(p => p.Name == "path").IsRequired);
        }
    }
}
