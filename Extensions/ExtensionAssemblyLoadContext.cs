using System.Runtime.Loader;

namespace OLAF.Extensions;

internal sealed class ExtensionAssemblyLoadContext : AssemblyLoadContext, IDisposable
{
    public ExtensionAssemblyLoadContext()
        : base($"OLAF.Extensions.{Guid.NewGuid():N}", isCollectible: true)
    {
    }

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName) => null;

    public void Dispose() => Unload();
}