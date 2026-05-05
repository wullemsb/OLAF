using Microsoft.Extensions.AI;

namespace OLAF.Extensions;

public interface IOlafExtension
{
    string Name { get; }

    IReadOnlyList<AIFunction> GetTools(ExtensionContributionContext context);
}