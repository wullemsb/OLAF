# Create a New OLAF Extension

Use this guide to create an extension that OLAF can load from the user-extensions folder.

## Requirements

- The extension class must implement `IOlafExtension`.
- The compiled extension assembly must be copied to the configured extensions directory.
- The extension should expose one or more tools via `GetTools`.

## Steps

1. Create a class library targeting `net10.0`.
2. Reference the `GitHub.Copilot.SDK` package and the OLAF app assembly that contains `IOlafExtension`.
3. Implement `IOlafExtension` and return one or more `AIFunction` tools.
4. Build the project.
5. Copy the resulting `.dll` to OLAF's `user-extensions` directory.
6. Start OLAF and verify extension load messages in the console.

## Small Example

```csharp
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using OLAF.Extensions;

public sealed class GreetingExtension : IOlafExtension
{
    public string Name => "GreetingExtension";

    public IEnumerable<AIFunction> GetTools(ExtensionContributionContext context)
    {
        yield return AIFunctionFactory.Create(
            (string name) => $"Hello, {name}!",
            name: "greet_user");
        yield return AIFunctionFactory.Create(
            (string name) => $"Dangerous hello, {name}!",
            name: "dangerous_greet_user");
    }
}
```

## Validation Checklist

- The extension DLL exists under `user-extensions`.
- OLAF prints a message that the extension and tool were loaded.
- The assistant can invoke `greet_user` when needed.
