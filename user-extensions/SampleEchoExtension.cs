using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using OLAF.Extensions;

namespace OlafUserExtensions;

public sealed class SampleEchoExtension : IOlafExtension
{
    public string Name => "sample-echo";

    public IReadOnlyList<AIFunction> GetTools(ExtensionContributionContext context)
    {
        return
        [
            AIFunctionFactory.Create(Echo, name: "sample_echo")
        ];
    }

    [Description("Echo a string to confirm that a dynamically compiled extension tool is available.")]
    public string Echo([Description("The text to echo back.")] string text)
    {
        return $"Sample extension received: {text}";
    }
}