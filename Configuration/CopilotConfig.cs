namespace OLAF.Configuration;

public sealed class CopilotConfig
{
    public const string SectionName = "Copilot";

    public string Model { get; init; } = "gpt-5";

    public string? GitHubToken { get; init; }

    public bool EnableExtensions { get; init; } = true;

    public string ExtensionsDirectory { get; init; } = "user-extensions";

    public bool EnableSkills { get; init; } = true;

    public string SkillsDirectory { get; init; } = "skills";

    public string SystemPrompt { get; init; } =
        "You are an expert AI coding assistant for .NET developers. " +
        "You help with code reviews, explaining concepts, writing code, " +
        "and debugging. You have tools to read files and list directories " +
        "when the user wants you to analyze their code. " +
        "Be concise but thorough. Use C# code examples where helpful.";
}