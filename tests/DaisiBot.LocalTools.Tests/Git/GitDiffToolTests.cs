using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Git;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Git
{
    public class GitDiffToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new GitDiffTool();
            Assert.Equal("daisi-git-diff", tool.Id);
        }

        [Fact]
        public async Task Execute_ModifiedFile_ReturnsDiff()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"git_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                GitTestHelper.RunGit("init", tempDir);
                GitTestHelper.RunGit("config user.email \"test@test.com\"", tempDir);
                GitTestHelper.RunGit("config user.name \"Test\"", tempDir);
                var file = Path.Combine(tempDir, "test.txt");
                File.WriteAllText(file, "original");
                GitTestHelper.RunGit("add -A", tempDir);
                GitTestHelper.RunGit("commit -m \"initial\"", tempDir);
                File.WriteAllText(file, "modified");

                var tool = new GitDiffTool();
                var context = new MockToolContext();
                var parameters = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempDir, IsRequired = true }
                };

                var execContext = tool.GetExecutionContext(context, CancellationToken.None, parameters);
                var result = await execContext.ExecutionTask;

                Assert.True(result.Success);
                Assert.Contains("modified", result.Output);
            }
            finally
            {
                GitTestHelper.ForceDeleteDirectory(tempDir);
            }
        }


    }
}
