using Daisi.SDK.Models.Tools;
using DaisiBot.LocalTools.Git;
using DaisiBot.LocalTools.Tests.Helpers;

namespace DaisiBot.LocalTools.Tests.Git
{
    public class GitBranchToolTests
    {
        [Fact]
        public void Id_ReturnsExpectedValue()
        {
            var tool = new GitBranchTool();
            Assert.Equal("daisi-git-branch", tool.Id);
        }

        [Fact]
        public async Task Execute_ListAndCreate_Works()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"git_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            try
            {
                GitTestHelper.RunGit("init", tempDir);
                GitTestHelper.RunGit("config user.email \"test@test.com\"", tempDir);
                GitTestHelper.RunGit("config user.name \"Test\"", tempDir);
                File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");
                GitTestHelper.RunGit("add -A", tempDir);
                GitTestHelper.RunGit("commit -m \"initial\"", tempDir);

                var tool = new GitBranchTool();
                var context = new MockToolContext();

                var createParams = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempDir, IsRequired = true },
                    new() { Name = "action", Value = "create", IsRequired = false },
                    new() { Name = "name", Value = "test-branch", IsRequired = false }
                };
                var createExec = tool.GetExecutionContext(context, CancellationToken.None, createParams);
                var createResult = await createExec.ExecutionTask;
                Assert.True(createResult.Success);

                var listParams = new ToolParameterBase[]
                {
                    new() { Name = "path", Value = tempDir, IsRequired = true },
                    new() { Name = "action", Value = "list", IsRequired = false }
                };
                var listExec = tool.GetExecutionContext(context, CancellationToken.None, listParams);
                var listResult = await listExec.ExecutionTask;
                Assert.True(listResult.Success);
                Assert.Contains("test-branch", listResult.Output);
            }
            finally
            {
                GitTestHelper.ForceDeleteDirectory(tempDir);
            }
        }


    }
}
