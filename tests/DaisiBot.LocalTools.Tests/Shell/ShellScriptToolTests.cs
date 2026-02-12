using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Shell;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Shell
{
    public class ShellScriptToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new ShellScriptTool();
            Assert.Equal("daisi-shell-script", tool.Id);
        }

        [Fact]
        public async Task Execute_FileNotFound_ReturnsError()
        {
            var tool = new ShellScriptTool();
            var context = new MockToolContext();
            var parameters = new ToolParameterBase[]
            {
                new() { Name = "path", Value = @"C:\nonexistent\script.bat", IsRequired = true }
            };

            var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
            var result = await execContext.ExecutionTask;

            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task Execute_BatFile_RunsSuccessfully()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.bat");
            try
            {
                File.WriteAllText(tempFile, "@echo off\necho test_output");

                var tool = new ShellScriptTool();
                var context = new MockToolContext();
                var parameters = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempFile, IsRequired = true }
                };

                var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
                var result = await execContext.ExecutionTask;

                Assert.True(result.Success);
                Assert.Contains("test_output", result.Output);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
