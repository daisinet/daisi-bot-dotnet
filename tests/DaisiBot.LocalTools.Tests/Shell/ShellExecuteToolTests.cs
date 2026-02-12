using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Shell;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Shell
{
    public class ShellExecuteToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ShellExecuteTool();
            Assert.Equal("daisi-shell-execute", tool.Id);
        }

        [Fact]
        public void Parameters_CommandIsRequired()
        {
            var tool = new ShellExecuteTool();
            Assert.True(tool.Parameters.First(p => p.Name == "command").IsRequired);
        }

        [Fact]
        public async Task Execute_EchoHello_ReturnsOutput()
        {
            var tool = new ShellExecuteTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "command", Value = "echo hello", IsRequired = true }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
            Assert.Contains("hello", result.Output);
        }

        [Fact]
        public async Task Execute_InvalidCommand_ReturnsFailure()
        {
            var tool = new ShellExecuteTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "command", Value = "nonexistentcommand_xyz_12345", IsRequired = true }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Execute_WithWorkingDirectory_UsesDirectory()
        {
            var tool = new ShellExecuteTool();
            var context = new MockToolContext();
            var tempDir = Path.GetTempPath();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "command", Value = "cd", IsRequired = true },
                new() { Name = "working-directory", Value = tempDir, IsRequired = false }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.True(result.Success);
        }
    }
}
