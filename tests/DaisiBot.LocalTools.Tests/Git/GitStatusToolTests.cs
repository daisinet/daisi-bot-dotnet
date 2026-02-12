using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Git;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Git
{
    public class GitStatusToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new GitStatusTool();
            Assert.Equal("daisi-git-status", tool.Id);
        }

        [Fact]
        public async Task Execute_TempRepo_ReturnsStatus()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"git_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                GitTestHelper.RunGit("init", tempDir);

                var tool = new GitStatusTool();
                var context = new MockToolContext();
                var parameters = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempDir, IsRequired = true }
                };

                var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
                var result = await execContext.ExecutionTask;

                Assert.True(result.Success);
                Assert.Contains("branch", result.Output, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                GitTestHelper.ForceDeleteDirectory(tempDir);
            }
        }


    }
}
