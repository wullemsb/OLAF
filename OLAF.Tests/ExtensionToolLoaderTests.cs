using OLAF.Configuration;
using OLAF.Extensions;

namespace OLAF.Tests;

public sealed class ExtensionToolLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ExtensionToolLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OLAFExtTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void LoadTools_ReturnsDisabledMessage_WhenExtensionsDisabled()
    {
        var config = new CopilotConfig { EnableExtensions = false };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.Empty(result.Tools);
        Assert.Equal(0, result.ExtensionCount);
        Assert.Single(result.Messages);
        Assert.Equal(ExtensionLoadSeverity.Info, result.Messages[0].Severity);
        Assert.Contains("disabled", result.Messages[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadTools_ReturnsEmpty_WhenNoSourceFiles()
    {
        var extensionsDir = Path.Combine(_tempDir, "empty-extensions");
        Directory.CreateDirectory(extensionsDir);
        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = extensionsDir };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.Empty(result.Tools);
        Assert.Equal(0, result.ExtensionCount);
    }

    [Fact]
    public void LoadTools_CreatesExtensionDirectory_WhenItDoesNotExist()
    {
        var extensionsDir = Path.Combine(_tempDir, "auto-created");
        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = extensionsDir };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.True(Directory.Exists(extensionsDir));
        Assert.Contains(result.Messages, m => m.Message.Contains("Created extension directory"));
    }

    [Fact]
    public void LoadTools_ReturnsCompilationErrors_WhenSourceFileHasSyntaxErrors()
    {
        var extensionsDir = Path.Combine(_tempDir, "bad-source");
        Directory.CreateDirectory(extensionsDir);
        File.WriteAllText(Path.Combine(extensionsDir, "bad.cs"), "this is not valid C# !!!@@@");

        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = extensionsDir };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.Empty(result.Tools);
        Assert.Equal(0, result.ExtensionCount);
        Assert.Contains(result.Messages, m => m.Severity == ExtensionLoadSeverity.Error);
    }

    [Fact]
    public void LoadTools_ReturnsEmpty_WhenSourceFileCompilesButHasNoExtensionTypes()
    {
        var extensionsDir = Path.Combine(_tempDir, "no-extension-types");
        Directory.CreateDirectory(extensionsDir);
        File.WriteAllText(Path.Combine(extensionsDir, "helper.cs"), "public static class Helper { public static int Add(int a, int b) => a + b; }");

        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = extensionsDir };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.Empty(result.Tools);
        Assert.Equal(0, result.ExtensionCount);
    }

    [Fact]
    public void LoadTools_LoadsExtension_WhenValidExtensionSourceProvided()
    {
        var extensionsDir = Path.Combine(_tempDir, "valid-extension");
        Directory.CreateDirectory(extensionsDir);

        var source = """
            using Microsoft.Extensions.AI;
            using OLAF.Extensions;

            public sealed class MyTestExtension : IOlafExtension
            {
                public string Name => "MyTestExtension";

                public System.Collections.Generic.IReadOnlyList<AIFunction> GetTools(ExtensionContributionContext context)
                    => [];
            }
            """;

        File.WriteAllText(Path.Combine(extensionsDir, "MyTestExtension.cs"), source);

        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = extensionsDir };
        var loader = new ExtensionToolLoader(_tempDir, config);

        using var result = loader.LoadTools();

        Assert.Equal(1, result.ExtensionCount);
        Assert.Contains(result.Messages, m => m.Message.Contains("MyTestExtension"));
    }

    [Fact]
    public void LoadTools_UsesAbsoluteExtensionsDirectory_WhenPathIsRooted()
    {
        var absoluteExtDir = Path.Combine(_tempDir, "absolute-ext");
        Directory.CreateDirectory(absoluteExtDir);

        var config = new CopilotConfig { EnableExtensions = true, ExtensionsDirectory = absoluteExtDir };
        var loader = new ExtensionToolLoader("C:\\some\\other\\base", config);

        using var result = loader.LoadTools();

        Assert.True(Directory.Exists(absoluteExtDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
