using DaisiBot.Agent.Skills;
using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Tests.Skills;

public class SkillFileLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public SkillFileLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"skill-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateCategory(string category)
    {
        var dir = Path.Combine(_tempDir, category);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task LoadAllAsync_EmptyDirs_ReturnsEmptyList()
    {
        CreateCategory("builtin");
        CreateCategory("community");
        CreateCategory("custom");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAllAsync_MarkdownWithFrontmatter_ReturnsCorrectSkill()
    {
        var dir = CreateCategory("builtin");
        var content = """
            ---
            name: Test Skill
            description: A test skill for unit testing.
            shortDescription: Test skill
            version: "2.0.0"
            author: TestAuthor
            tags:
              - testing
              - demo
            tools:
              - CodingTools
              - FileTools
            iconUrl: https://example.com/icon.png
            ---

            You are a test skill. Do test things.
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "test-skill.md"), content);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadAllAsync();

        Assert.Single(result);
        var skill = result[0];
        Assert.Equal("builtin/test-skill", skill.Id);
        Assert.Equal("Test Skill", skill.Name);
        Assert.Equal("A test skill for unit testing.", skill.Description);
        Assert.Equal("Test skill", skill.ShortDescription);
        Assert.Equal("2.0.0", skill.Version);
        Assert.Equal("TestAuthor", skill.Author);
        Assert.Equal("https://example.com/icon.png", skill.IconUrl);
        Assert.Contains("You are a test skill. Do test things.", skill.SystemPromptTemplate);
        Assert.Equal(SkillVisibility.Public, skill.Visibility);
        Assert.Equal(SkillStatus.Approved, skill.Status);
        Assert.Contains(ToolGroupSelection.CodingTools, skill.RequiredToolGroups);
        Assert.Contains(ToolGroupSelection.FileTools, skill.RequiredToolGroups);
        Assert.Contains("testing", skill.Tags);
        Assert.Contains("demo", skill.Tags);
    }

    [Fact]
    public async Task LoadAllAsync_MultipleCategories_LoadsAll()
    {
        var builtin = CreateCategory("builtin");
        var custom = CreateCategory("custom");
        await File.WriteAllTextAsync(Path.Combine(builtin, "a.txt"), "Skill A");
        await File.WriteAllTextAsync(Path.Combine(custom, "b.txt"), "Skill B");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == "builtin/a");
        Assert.Contains(result, s => s.Id == "custom/b");
    }

    [Fact]
    public async Task LoadFromCategoryAsync_FiltersSingleCategory()
    {
        var builtin = CreateCategory("builtin");
        var custom = CreateCategory("custom");
        await File.WriteAllTextAsync(Path.Combine(builtin, "a.txt"), "Skill A");
        await File.WriteAllTextAsync(Path.Combine(custom, "b.txt"), "Skill B");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("custom");

        Assert.Single(result);
        Assert.Equal("custom/b", result[0].Id);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_TxtImport_NameFromFilenameBodyFromContent()
    {
        var dir = CreateCategory("community");
        await File.WriteAllTextAsync(Path.Combine(dir, "my-custom-skill.txt"), "Do custom things.\nBe helpful.");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        var skill = result[0];
        Assert.Equal("community/my-custom-skill", skill.Id);
        Assert.Equal("my-custom-skill", skill.Name);
        Assert.Equal("Do custom things.\nBe helpful.", skill.SystemPromptTemplate);
        Assert.Equal(SkillVisibility.Public, skill.Visibility);
        Assert.Equal(SkillStatus.Approved, skill.Status);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_JsonImport_MetadataAndPrompt()
    {
        var dir = CreateCategory("community");
        var json = """
            {
                "name": "JSON Skill",
                "description": "A skill from JSON",
                "shortDescription": "JSON skill",
                "version": "1.2.0",
                "author": "JsonAuthor",
                "tags": ["json", "import"],
                "tools": ["InformationTools"],
                "prompt": "You are a JSON-imported skill."
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "json-skill.json"), json);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        var skill = result[0];
        Assert.Equal("community/json-skill", skill.Id);
        Assert.Equal("JSON Skill", skill.Name);
        Assert.Equal("A skill from JSON", skill.Description);
        Assert.Equal("1.2.0", skill.Version);
        Assert.Equal("You are a JSON-imported skill.", skill.SystemPromptTemplate);
        Assert.Contains("json", skill.Tags);
        Assert.Contains(ToolGroupSelection.InformationTools, skill.RequiredToolGroups);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_JsonImport_SystemPromptTemplateField()
    {
        var dir = CreateCategory("community");
        var json = """
            {
                "name": "Alt JSON",
                "systemPromptTemplate": "Use systemPromptTemplate field."
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "alt.json"), json);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        Assert.Equal("Use systemPromptTemplate field.", result[0].SystemPromptTemplate);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_JsonImport_InstructionsField()
    {
        var dir = CreateCategory("community");
        var json = """
            {
                "name": "Instructions JSON",
                "instructions": "Use instructions field."
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "instr.json"), json);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        Assert.Equal("Use instructions field.", result[0].SystemPromptTemplate);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_MissingDirectory_ReturnsEmpty()
    {
        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("nonexistent");

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAllAsync_MissingBaseDirectory_ReturnsEmpty()
    {
        var loader = new SkillFileLoader(Path.Combine(_tempDir, "does-not-exist"));
        var result = await loader.LoadAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAllAsync_AllFileSkillsGetApprovedPublicStatus()
    {
        var dir = CreateCategory("builtin");
        await File.WriteAllTextAsync(Path.Combine(dir, "a.txt"), "A");
        await File.WriteAllTextAsync(Path.Combine(dir, "b.md"), "---\nname: B\n---\nB body");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadAllAsync();

        Assert.All(result, skill =>
        {
            Assert.Equal(SkillVisibility.Public, skill.Visibility);
            Assert.Equal(SkillStatus.Approved, skill.Status);
        });
    }

    [Fact]
    public async Task LoadFromCategoryAsync_YamlImport_MetadataAndPrompt()
    {
        var dir = CreateCategory("community");
        var yaml = """
            name: YAML Skill
            description: A skill from YAML
            version: "1.1.0"
            author: YamlAuthor
            tags:
              - yaml
            tools:
              - CodingTools
            prompt: You are a YAML-imported skill.
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "yaml-skill.yaml"), yaml);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        var skill = result[0];
        Assert.Equal("community/yaml-skill", skill.Id);
        Assert.Equal("YAML Skill", skill.Name);
        Assert.Equal("A skill from YAML", skill.Description);
        Assert.Equal("1.1.0", skill.Version);
        Assert.Equal("You are a YAML-imported skill.", skill.SystemPromptTemplate);
        Assert.Contains(ToolGroupSelection.CodingTools, skill.RequiredToolGroups);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_YmlExtension_Works()
    {
        var dir = CreateCategory("community");
        var yaml = """
            name: YML Skill
            prompt: YML prompt body.
            """;
        await File.WriteAllTextAsync(Path.Combine(dir, "yml-skill.yml"), yaml);

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("community");

        Assert.Single(result);
        Assert.Equal("YML Skill", result[0].Name);
        Assert.Equal("YML prompt body.", result[0].SystemPromptTemplate);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_UnsupportedExtension_Ignored()
    {
        var dir = CreateCategory("builtin");
        await File.WriteAllTextAsync(Path.Combine(dir, "readme.html"), "<h1>Not a skill</h1>");
        await File.WriteAllTextAsync(Path.Combine(dir, "valid.txt"), "A real skill");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("builtin");

        Assert.Single(result);
        Assert.Equal("builtin/valid", result[0].Id);
    }

    // --- Static helper tests ---

    [Fact]
    public void ParseFrontmatter_ValidFrontmatter_ReturnsParsedData()
    {
        var content = "---\nname: My Skill\nversion: \"2.0\"\n---\nBody content here.";
        var (frontmatter, body) = SkillFileLoader.ParseFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.Equal("My Skill", frontmatter.Name);
        Assert.Equal("2.0", frontmatter.Version);
        Assert.Contains("Body content here.", body);
    }

    [Fact]
    public void ParseFrontmatter_MissingClosingDelimiter_ReturnsNullFrontmatter()
    {
        var content = "---\nname: My Skill\nNo closing delimiter";
        var (frontmatter, body) = SkillFileLoader.ParseFrontmatter(content);

        Assert.Null(frontmatter);
        Assert.Equal(content, body);
    }

    [Fact]
    public void ParseFrontmatter_NoFrontmatter_ReturnsNullAndFullContent()
    {
        var content = "Just regular markdown content.";
        var (frontmatter, body) = SkillFileLoader.ParseFrontmatter(content);

        Assert.Null(frontmatter);
        Assert.Equal(content, body);
    }

    [Fact]
    public void ParseFrontmatter_EmptyFrontmatter_ReturnsDefaultObject()
    {
        var content = "---\n---\nBody only.";
        var (frontmatter, body) = SkillFileLoader.ParseFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.Equal(string.Empty, frontmatter.Name);
        Assert.Contains("Body only.", body);
    }

    [Fact]
    public void BuildId_FormatsCorrectly()
    {
        Assert.Equal("builtin/summarize", SkillFileLoader.BuildId("builtin", "summarize"));
        Assert.Equal("custom/my-skill", SkillFileLoader.BuildId("custom", "my-skill"));
    }

    [Fact]
    public void ParseToolGroups_ValidNames_ReturnsEnumValues()
    {
        var result = SkillFileLoader.ParseToolGroups(["CodingTools", "FileTools", "InformationTools"]);

        Assert.Equal(3, result.Count);
        Assert.Contains(ToolGroupSelection.CodingTools, result);
        Assert.Contains(ToolGroupSelection.FileTools, result);
        Assert.Contains(ToolGroupSelection.InformationTools, result);
    }

    [Fact]
    public void ParseToolGroups_InvalidNames_SilentlySkipped()
    {
        var result = SkillFileLoader.ParseToolGroups(["CodingTools", "NonExistentTool", "MadeUpTools"]);

        Assert.Single(result);
        Assert.Contains(ToolGroupSelection.CodingTools, result);
    }

    [Fact]
    public void ParseToolGroups_EmptyList_ReturnsEmpty()
    {
        var result = SkillFileLoader.ParseToolGroups([]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseToolGroups_CaseInsensitive()
    {
        var result = SkillFileLoader.ParseToolGroups(["codingtools", "FILETOOLS"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(ToolGroupSelection.CodingTools, result);
        Assert.Contains(ToolGroupSelection.FileTools, result);
    }

    [Fact]
    public async Task LoadFromCategoryAsync_MarkdownWithoutFrontmatter_UsesFilenameAsName()
    {
        var dir = CreateCategory("builtin");
        await File.WriteAllTextAsync(Path.Combine(dir, "plain.md"), "Just some instructions without frontmatter.");

        var loader = new SkillFileLoader(_tempDir);
        var result = await loader.LoadFromCategoryAsync("builtin");

        Assert.Single(result);
        Assert.Equal("plain", result[0].Name);
        Assert.Equal("Just some instructions without frontmatter.", result[0].SystemPromptTemplate);
    }
}
