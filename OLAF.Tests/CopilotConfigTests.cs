using OLAF.Configuration;

namespace OLAF.Tests;

public sealed class CopilotConfigTests
{
    [Fact]
    public void SectionName_IsCorrect()
    {
        Assert.Equal("Copilot", CopilotConfig.SectionName);
    }

    [Fact]
    public void DefaultModel_IsGpt5()
    {
        var config = new CopilotConfig();

        Assert.Equal("gpt-5", config.Model);
    }

    [Fact]
    public void DefaultGitHubToken_IsNull()
    {
        var config = new CopilotConfig();

        Assert.Null(config.GitHubToken);
    }

    [Fact]
    public void EnableExtensions_DefaultsToTrue()
    {
        var config = new CopilotConfig();

        Assert.True(config.EnableExtensions);
    }

    [Fact]
    public void ExtensionsDirectory_DefaultsToUserExtensions()
    {
        var config = new CopilotConfig();

        Assert.Equal("user-extensions", config.ExtensionsDirectory);
    }

    [Fact]
    public void EnableSkills_DefaultsToTrue()
    {
        var config = new CopilotConfig();

        Assert.True(config.EnableSkills);
    }

    [Fact]
    public void SkillsDirectory_DefaultsToSkills()
    {
        var config = new CopilotConfig();

        Assert.Equal("skills", config.SkillsDirectory);
    }

    [Fact]
    public void SystemPrompt_IsNotNullOrEmpty()
    {
        var config = new CopilotConfig();

        Assert.False(string.IsNullOrWhiteSpace(config.SystemPrompt));
    }

    [Fact]
    public void InitProperties_CanBeOverridden()
    {
        var config = new CopilotConfig
        {
            Model = "gpt-4o",
            GitHubToken = "tok_test",
            EnableExtensions = false,
            EnableSkills = false,
            ExtensionsDirectory = "/custom/ext",
            SkillsDirectory = "/custom/skills",
            SystemPrompt = "Custom prompt"
        };

        Assert.Equal("gpt-4o", config.Model);
        Assert.Equal("tok_test", config.GitHubToken);
        Assert.False(config.EnableExtensions);
        Assert.False(config.EnableSkills);
        Assert.Equal("/custom/ext", config.ExtensionsDirectory);
        Assert.Equal("/custom/skills", config.SkillsDirectory);
        Assert.Equal("Custom prompt", config.SystemPrompt);
    }
}
