using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Git;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Git
{
    public class GitCommitToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new GitCommitTool();
            Assert.Equal("daisi-git-commit", tool.Id);
        }

        [Fact]
        public async Task Execute_StageAndCommit_Works()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"git_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                GitTestHelper.RunGit("init", tempDir);
                GitTestHelper.RunGit("config user.email \"test@test.com\"", tempDir);
                GitTestHelper.RunGit("config user.name \"Test\"", tempDir);
                File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");

                var tool = new GitCommitTool();
                var context = new MockToolContext();
                var parameters = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempDir, IsRequired = true },
                    new() { Name = "message", Value = "test commit", IsRequired = true }
                };

                var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
                var result = await execContext.ExecutionTask;

                Assert.True(result.Success);
                Assert.Contains("test commit", result.Output);
            }
            finally
            {
                GitTestHelper.ForceDeleteDirectory(tempDir);
            }
        }


    }
}
