using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.SystemInfo
{
    public class SystemInfoToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemInfoTool();
            Assert.Equal("daisi-system-info", tool.Id);
        }

        [Fact]
        public async Task Execute_AllCategory_ReturnsJsonWithOsAndCpu()
        {
            var tool = new DaisiBot.LocalTools.SystemInfo.SystemInfoTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "category", Value = "all", IsRequired = false }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.Contains("os", result.Output);
            Assert.Contains("cpu", result.Output);
        }
    }
}
