using OLAF.Extensions;

namespace OLAF.Tests;

public sealed class ExtensionLoadMessageTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var msg = new ExtensionLoadMessage(ExtensionLoadSeverity.Warning, "something went wrong");

        Assert.Equal(ExtensionLoadSeverity.Warning, msg.Severity);
        Assert.Equal("something went wrong", msg.Message);
    }

    [Theory]
    [InlineData(ExtensionLoadSeverity.Info)]
    [InlineData(ExtensionLoadSeverity.Warning)]
    [InlineData(ExtensionLoadSeverity.Error)]
    public void Severity_CanBeAllValues(ExtensionLoadSeverity severity)
    {
        var msg = new ExtensionLoadMessage(severity, "msg");

        Assert.Equal(severity, msg.Severity);
    }

    [Fact]
    public void Records_SupportValueEquality()
    {
        var msg1 = new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "test");
        var msg2 = new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "test");

        Assert.Equal(msg1, msg2);
    }

    [Fact]
    public void Records_AreNotEqual_WhenSeverityDiffers()
    {
        var msg1 = new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "test");
        var msg2 = new ExtensionLoadMessage(ExtensionLoadSeverity.Error, "test");

        Assert.NotEqual(msg1, msg2);
    }

    [Fact]
    public void Records_AreNotEqual_WhenMessageDiffers()
    {
        var msg1 = new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "msg-a");
        var msg2 = new ExtensionLoadMessage(ExtensionLoadSeverity.Info, "msg-b");

        Assert.NotEqual(msg1, msg2);
    }
}
