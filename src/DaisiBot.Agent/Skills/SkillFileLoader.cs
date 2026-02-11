using System.Text.Json;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models.Skills;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SdkSkillFileLoader = Daisi.SDK.Skills.SkillFileLoader;

namespace DaisiBot.Agent.Skills;

public class SkillFileLoader : ISkillFileLoader
{
    private static readonly string[] Categories = ["builtin", "community", "custom"];
    private static readonly string[] SupportedExtensions = [".md", ".txt", ".json", ".yaml", ".yml"];

    private readonly string _basePath;

    public SkillFileLoader(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "daisi-skills");
    }

    public async Task<List<Skill>> LoadAllAsync(CancellationToken ct = default)
    {
        var skills = new List<Skill>();
        foreach (var category in Categories)
        {
            ct.ThrowIfCancellationRequested();
            skills.AddRange(await LoadFromCategoryAsync(category, ct));
        }
        return skills;
    }

    public async Task<List<Skill>> LoadFromCategoryAsync(string category, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, category);
        if (!Directory.Exists(dir))
            return [];

        var skills = new List<Skill>();
        var files = Directory.EnumerateFiles(dir)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var skill = await LoadFileAsync(file, category, ct);
            if (skill is not null)
                skills.Add(skill);
        }

        return skills;
    }

    private async Task<Skill?> LoadFileAsync(string filePath, string category, CancellationToken ct)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var content = await File.ReadAllTextAsync(filePath, ct);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        return ext switch
        {
            ".md" => LoadMarkdown(content, fileNameWithoutExt, category),
            ".txt" => LoadText(content, fileNameWithoutExt, category),
            ".json" => LoadJson(content, fileNameWithoutExt, category),
            ".yaml" or ".yml" => LoadYaml(content, fileNameWithoutExt, category),
            _ => null
        };
    }

    private static Skill LoadMarkdown(string content, string fileName, string category)
    {
        // Delegate frontmatter parsing to the shared SDK parser
        var sdkSkill = SdkSkillFileLoader.LoadMarkdown(content, BuildId(category, fileName));

        var skill = new Skill
        {
            Id = sdkSkill.Id,
            Name = string.IsNullOrWhiteSpace(sdkSkill.Name) ? fileName : sdkSkill.Name,
            Description = sdkSkill.Description,
            ShortDescription = sdkSkill.ShortDescription,
            Version = sdkSkill.Version,
            Author = sdkSkill.Author,
            Tags = sdkSkill.Tags,
            IconUrl = sdkSkill.IconUrl,
            SystemPromptTemplate = sdkSkill.SystemPromptTemplate,
            RequiredToolGroups = ParseToolGroups(sdkSkill.RequiredToolGroups),
            Visibility = SkillVisibility.Public,
            Status = SkillStatus.Approved
        };

        return skill;
    }

    private static Skill LoadText(string content, string fileName, string category)
    {
        return new Skill
        {
            Id = BuildId(category, fileName),
            Name = fileName,
            SystemPromptTemplate = content.Trim(),
            Visibility = SkillVisibility.Public,
            Status = SkillStatus.Approved
        };
    }

    private static Skill LoadJson(string content, string fileName, string category)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var skill = new Skill
        {
            Id = BuildId(category, fileName),
            Name = GetJsonString(root, "name") ?? fileName,
            Description = GetJsonString(root, "description") ?? string.Empty,
            ShortDescription = GetJsonString(root, "shortDescription") ?? string.Empty,
            Version = GetJsonString(root, "version") ?? "1.0.0",
            Author = GetJsonString(root, "author") ?? string.Empty,
            IconUrl = GetJsonString(root, "iconUrl") ?? string.Empty,
            Visibility = SkillVisibility.Public,
            Status = SkillStatus.Approved
        };

        // Extract prompt body from known field names
        skill.SystemPromptTemplate = (
            GetJsonString(root, "prompt")
            ?? GetJsonString(root, "systemPromptTemplate")
            ?? GetJsonString(root, "instructions")
            ?? string.Empty
        ).Trim();

        if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            skill.Tags = tagsEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();

        if (root.TryGetProperty("tools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
        {
            var toolNames = toolsEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
            skill.RequiredToolGroups = ParseToolGroups(toolNames);
        }

        return skill;
    }

    private static Skill LoadYaml(string content, string fileName, string category)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var data = deserializer.Deserialize<Dictionary<string, object?>>(content) ?? [];

        var skill = new Skill
        {
            Id = BuildId(category, fileName),
            Name = GetYamlString(data, "name") ?? fileName,
            Description = GetYamlString(data, "description") ?? string.Empty,
            ShortDescription = GetYamlString(data, "shortDescription") ?? string.Empty,
            Version = GetYamlString(data, "version") ?? "1.0.0",
            Author = GetYamlString(data, "author") ?? string.Empty,
            IconUrl = GetYamlString(data, "iconUrl") ?? string.Empty,
            SystemPromptTemplate = (GetYamlString(data, "prompt") ?? string.Empty).Trim(),
            Visibility = SkillVisibility.Public,
            Status = SkillStatus.Approved
        };

        if (data.TryGetValue("tags", out var tagsObj) && tagsObj is List<object> tagsList)
            skill.Tags = tagsList.Select(t => t?.ToString() ?? string.Empty).Where(t => t.Length > 0).ToList();

        if (data.TryGetValue("tools", out var toolsObj) && toolsObj is List<object> toolsList)
            skill.RequiredToolGroups = ParseToolGroups(
                toolsList.Select(t => t?.ToString() ?? string.Empty).Where(t => t.Length > 0).ToList());

        return skill;
    }

    internal static (SkillFrontmatter? Frontmatter, string Body) ParseFrontmatter(string content)
    {
        // Delegate to the shared SDK parser, then map to bot's SkillFrontmatter type
        var (sdkFrontmatter, body) = SdkSkillFileLoader.ParseFrontmatter(content);
        if (sdkFrontmatter is null)
            return (null, body);

        var frontmatter = new SkillFrontmatter
        {
            Name = sdkFrontmatter.Name,
            Description = sdkFrontmatter.Description,
            ShortDescription = sdkFrontmatter.ShortDescription,
            Version = sdkFrontmatter.Version,
            Author = sdkFrontmatter.Author,
            Tags = sdkFrontmatter.Tags,
            Tools = sdkFrontmatter.Tools,
            IconUrl = sdkFrontmatter.IconUrl,
            IsRequired = sdkFrontmatter.IsRequired
        };
        return (frontmatter, body);
    }

    internal static string BuildId(string category, string fileName)
    {
        return $"{category}/{fileName}";
    }

    internal static List<ToolGroupSelection> ParseToolGroups(List<string> toolNames)
    {
        var result = new List<ToolGroupSelection>();
        foreach (var name in toolNames)
        {
            if (Enum.TryParse<ToolGroupSelection>(name, ignoreCase: true, out var parsed))
                result.Add(parsed);
        }
        return result;
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static string? GetYamlString(Dictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out var val) ? val?.ToString() : null;
    }
}
