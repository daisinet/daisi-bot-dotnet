using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.SystemInfo
{
    public class SystemEnvToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemEnvTool();
            Assert.Equal("daisi-system-env", tool.Id);
        }

        [Fact]
        public async Task Execute_PathVariable_ReturnsValue()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemEnvTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "name", Value = "PATH", IsRequired = false }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.Contains("PATH", result.Output);
        }
    }
}
