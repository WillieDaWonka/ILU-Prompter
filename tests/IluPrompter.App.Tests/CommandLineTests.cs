namespace IluPrompter.App.Tests;

/// <summary>Command-line handling added at integration: a script path to open and the dev --settings-file flag.</summary>
public class CommandLineTests
{
    [Fact]
    public void ScriptPath_IsTheFirstArgumentThatIsNotAFlagOrAFlagValue()
    {
        var path = App.FindScriptPath(["--diag", "--window-size", "1366x768", "--settings-file", "x.json", "--dev-output-on-primary", @"C:\scripts\show.ilup"]);
        Assert.Equal(@"C:\scripts\show.ilup", path);
    }

    [Fact]
    public void ScriptPath_IsNullWithoutOne()
    {
        Assert.Null(App.FindScriptPath(["--diag", "--settings-file", "x.json"]));
        Assert.Null(App.FindScriptPath([]));
    }

    [Fact]
    public void ScriptPath_IsMadeAbsolute()
    {
        Assert.Equal(System.IO.Path.GetFullPath("samples/sample-short.txt"), App.FindScriptPath(["samples/sample-short.txt"]));
    }

    [Fact]
    public void Option_ReturnsTheNextArgument_UnlessItIsAnotherFlag()
    {
        Assert.Equal("a.json", App.TryGetOption(["--Settings-File", "a.json"], "--settings-file"));
        Assert.Null(App.TryGetOption(["--settings-file", "--diag"], "--settings-file"));
        Assert.Null(App.TryGetOption(["--settings-file"], "--settings-file"));
        Assert.Null(App.TryGetOption(["--diag"], "--settings-file"));
    }
}
