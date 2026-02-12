using DaisiBot.LocalTools.Input;

namespace DaisiBot.LocalTools.Tests.Input
{
    public class InputKeyToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new InputKeyTool();
            Assert.Equal("daisi-input-key", tool.Id);
        }

        [Theory]
        [InlineData("ctrl", 0x11)]
        [InlineData("alt", 0x12)]
        [InlineData("shift", 0x10)]
        [InlineData("enter", 0x0D)]
        [InlineData("tab", 0x09)]
        [InlineData("escape", 0x1B)]
        [InlineData("f1", 0x70)]
        [InlineData("a", 0x41)]
        public void MapKeyName_KnownKeys_ReturnsCorrectVkCode(string name, int expected)
        {
            Assert.Equal((ushort)expected, InputKeyTool.MapKeyName(name));
        }

        [Fact]
        public void MapKeyName_UnknownKey_ReturnsZero()
        {
            Assert.Equal((ushort)0, InputKeyTool.MapKeyName("unknownkey"));
        }
    }
}
