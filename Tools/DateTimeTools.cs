using System.ComponentModel;

namespace OLAF.Tools;

public sealed class DateTimeTools
{
    [Description("Get the current local date and time.")]
    public string GetCurrentTime() =>
        DateTimeOffset.Now.ToString("dddd, MMMM d, yyyy h:mm:ss tt zzz");
}
