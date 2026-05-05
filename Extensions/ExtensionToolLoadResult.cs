using Microsoft.Extensions.AI;

namespace OLAF.Extensions;

public sealed class ExtensionToolLoadResult : IDisposable
{
    private readonly ExtensionAssemblyLoadContext? _loadContext;

    internal ExtensionToolLoadResult(
        IReadOnlyList<AIFunction> tools,
        IReadOnlyList<ExtensionLoadMessage> messages,
        int extensionCount,
        ExtensionAssemblyLoadContext? loadContext)
    {
        Tools = tools;
        Messages = messages;
        ExtensionCount = extensionCount;
        _loadContext = loadContext;
    }

    public IReadOnlyList<AIFunction> Tools { get; }

    public IReadOnlyList<ExtensionLoadMessage> Messages { get; }

    public int ExtensionCount { get; }

    public void Dispose() => _loadContext?.Dispose();
}