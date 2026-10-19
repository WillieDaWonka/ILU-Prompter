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

    [Fact]
    public void TheDemoInputFlag_IsNotAScriptPath_AndIsNotOnByDefault()
    {
        Assert.Null(App.FindScriptPath(["--demo-input"]));
        Assert.DoesNotContain("--demo-input", Environment.GetCommandLineArgs());
    }

    [Fact]
    public void TheDemoScript_OnlyEverUsesTheWheelOverThePreview_NoDevice()
    {
        var steps = IluPrompter.App.Services.DemoInput.Script(1);
        Assert.NotEmpty(steps);
        Assert.All(steps, step =>
        {
            Assert.Equal(IluPrompter.Core.ControllerKind.MouseWheel, step.Input.Source.Kind);
            Assert.Equal(IluPrompter.Core.AppSources.App, step.Input.Source.DeviceId);
        });
        Assert.True(steps.Max(s => s.AtMs) < IluPrompter.App.Services.DemoInput.CycleMs);
        // balanced: the wheel returns to where it started and every button that goes down comes up
        Assert.Equal(0, steps.Select(s => s.Input).OfType<IluPrompter.Core.RelativeInput>().Sum(r => r.Amount));
        foreach (var id in steps.Select(s => s.Input).OfType<IluPrompter.Core.ButtonInput>().Select(b => b.ControlId).Distinct())
        {
            var edges = steps.Select(s => s.Input).OfType<IluPrompter.Core.ButtonInput>().Where(b => b.ControlId == id).ToList();
            Assert.Equal(edges.Count(b => b.IsPressed), edges.Count(b => !b.IsPressed));
        }
    }
}
