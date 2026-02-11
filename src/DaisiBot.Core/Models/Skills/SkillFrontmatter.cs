using YamlDotNet.Serialization;

namespace DaisiBot.Core.Models.Skills;

public class SkillFrontmatter
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "shortDescription")]
    public string ShortDescription { get; set; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "1.0.0";

    [YamlMember(Alias = "author")]
    public string Author { get; set; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "tools")]
    public List<string> Tools { get; set; } = [];

    [YamlMember(Alias = "iconUrl")]
    public string IconUrl { get; set; } = string.Empty;

    [YamlMember(Alias = "isRequired")]
    public bool IsRequired { get; set; } = false;
}
