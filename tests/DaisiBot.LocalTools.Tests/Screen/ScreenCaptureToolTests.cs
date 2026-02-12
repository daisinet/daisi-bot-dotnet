using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Screen;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Screen
{
    public class ScreenCaptureToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ScreenCaptureTool();
            Assert.Equal("daisi-screen-capture", tool.Id);
        }

        [Fact]
        public void Parameters_ModeIsOptional()
        {
            var tool = new ScreenCaptureTool();
            Assert.False(tool.Parameters.First(p => p.Name == "mode").IsRequired);
        }

        [Fact]
        [Trait("Category", "Desktop")]
        public async Task Execute_FullCapture_ReturnsBase64()
        {
            var tool = new ScreenCaptureTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "mode", Value = "full", IsRequired = false }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Output));
            var bytes = Convert.FromBase64String(result.Output);
            Assert.True(bytes.Length > 0);
        }
    }
}
