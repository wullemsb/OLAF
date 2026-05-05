namespace OLAF.Extensions;

public enum ExtensionLoadSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ExtensionLoadMessage(ExtensionLoadSeverity Severity, string Message);