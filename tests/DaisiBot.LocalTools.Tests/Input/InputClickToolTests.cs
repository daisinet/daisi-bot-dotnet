using DaisiBot.LocalTools.Input;

namespace DaisiBot.LocalTools.Tests.Input
{
    public class InputClickToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new InputClickTool();
            Assert.Equal("daisi-input-click", tool.Id);
        }

        [Fact]
        public void Parameters_XIsRequired()
        {
            var tool = new InputClickTool();
            Assert.True(tool.Parameters.First(p => p.Name == "x").IsRequired);
        }

        [Fact]
        public void Parameters_YIsRequired()
        {
            var tool = new InputClickTool();
            Assert.True(tool.Parameters.First(p => p.Name == "y").IsRequired);
        }
    }
}
