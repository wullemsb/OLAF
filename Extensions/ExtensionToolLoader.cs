using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.AI;
using OLAF.Configuration;
using System.Collections.Immutable;
using System.Text;
using System.Reflection;

namespace OLAF.Extensions;

public sealed class ExtensionToolLoader
{
    private readonly string _applicationBaseDirectory;
    private readonly CopilotConfig _config;

    public ExtensionToolLoader(string applicationBaseDirectory, CopilotConfig config)
    {
        _applicationBaseDirectory = applicationBaseDirectory;
        _config = config;
    }

    public ExtensionToolLoadResult LoadTools()
    {
        if (!_config.EnableExtensions)
        {
            return new ExtensionToolLoadResult(
                tools: [],
                messages:
                [
                    new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "Extension loading is disabled.")
                ],
                extensionCount: 0,
                loadContext: null);
        }

        var extensionDirectory = ResolveExtensionDirectory();
        var messages = new List<ExtensionLoadMessage>();

        if (!Directory.Exists(extensionDirectory))
        {
            Directory.CreateDirectory(extensionDirectory);
            messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Info, $"Created extension directory at '{extensionDirectory}'."));
        }

        var sourceFiles = Directory.GetFiles(extensionDirectory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceFiles.Length == 0)
        {
            messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Info, $"No extension source files found in '{extensionDirectory}'."));

            return new ExtensionToolLoadResult([], messages, 0, null);
        }

        var compilation = CreateCompilation(sourceFiles);
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emitResult = compilation.Emit(
            peStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        if (!emitResult.Success)
        {
            AddCompilationDiagnostics(messages, emitResult.Diagnostics);
            return new ExtensionToolLoadResult([], messages, 0, null);
        }

        peStream.Position = 0;
        pdbStream.Position = 0;

        var loadContext = new ExtensionAssemblyLoadContext();

        try
        {
            var assembly = loadContext.LoadFromStream(peStream, pdbStream);
            var tools = new List<AIFunction>();
            var extensionCount = 0;
            var contributionContext = new ExtensionContributionContext(extensionDirectory);

            foreach (var type in assembly.GetTypes().Where(IsExtensionType))
            {
                if (Activator.CreateInstance(type) is not IOlafExtension extension)
                {
                    messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Error, $"Could not instantiate extension type '{type.FullName}'."));
                    continue;
                }

                IReadOnlyList<AIFunction> contributedTools;

                try
                {
                    contributedTools = extension.GetTools(contributionContext);
                }
                catch (Exception ex)
                {
                    messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Error, $"Extension '{extension.Name}' failed while building tools: {ex.Message}"));
                    continue;
                }

                extensionCount++;
                tools.AddRange(contributedTools);
                messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Info, $"Loaded extension '{extension.Name}' from '{type.Name}' with {contributedTools.Count} tool(s)."));
            }

            if (extensionCount == 0)
            {
                loadContext.Dispose();
                return new ExtensionToolLoadResult([], messages, 0, null);
            }

            return new ExtensionToolLoadResult(tools, messages, extensionCount, loadContext);
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.Where(exception => exception is not null))
            {
                messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Error, $"Extension assembly load failure: {loaderException!.Message}"));
            }

            loadContext.Dispose();
            return new ExtensionToolLoadResult([], messages, 0, null);
        }
        catch (Exception ex)
        {
            loadContext.Dispose();
            messages.Add(new ExtensionLoadMessage(ExtensionLoadSeverity.Error, $"Extension assembly load failed: {ex.Message}"));
            return new ExtensionToolLoadResult([], messages, 0, null);
        }
    }

    private string ResolveExtensionDirectory()
    {
        if (Path.IsPathRooted(_config.ExtensionsDirectory))
        {
            return Path.GetFullPath(_config.ExtensionsDirectory);
        }

        return Path.GetFullPath(Path.Combine(_applicationBaseDirectory, _config.ExtensionsDirectory));
    }

    private static bool IsExtensionType(Type type) =>
        typeof(IOlafExtension).IsAssignableFrom(type) &&
        type is { IsAbstract: false, IsInterface: false } &&
        type.GetConstructor(Type.EmptyTypes) is not null;

    private static CSharpCompilation CreateCompilation(IEnumerable<string> sourceFiles)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(path), Encoding.UTF8), parseOptions, path))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: $"OLAF.DynamicExtensions.{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                referencePaths.Add(path);
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location))
            {
                continue;
            }

            referencePaths.Add(assembly.Location);
        }
        //referencePaths.Add(System.AppContext.BaseDirectory);

        return [.. referencePaths.Select(static path => MetadataReference.CreateFromFile(path))];
    }

    private static void AddCompilationDiagnostics(List<ExtensionLoadMessage> messages, IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning))
        {
            var severity = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => ExtensionLoadSeverity.Error,
                DiagnosticSeverity.Warning => ExtensionLoadSeverity.Warning,
                _ => ExtensionLoadSeverity.Info
            };

            var lineSpan = diagnostic.Location.GetMappedLineSpan();
            var location = lineSpan.HasMappedPath
                ? $"{lineSpan.Path}({lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1})"
                : diagnostic.Location.ToString();

            messages.Add(new ExtensionLoadMessage(severity, $"{location}: {diagnostic.GetMessage()}"));
        }
    }
}