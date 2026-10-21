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
    public void TheDemoScript_OnlyEverUsesTheWheelOverThePreview_AndAMadeUpJogSource_NoDevice()
    {
        var steps = IluPrompter.App.Services.DemoInput.Script(1);
        Assert.NotEmpty(steps);
        Assert.All(steps, step =>
        {
            var source = step.Input.Source;
            var wheel = source.Kind == IluPrompter.Core.ControllerKind.MouseWheel && source.DeviceId == IluPrompter.Core.AppSources.App;
            var jog = source.Kind == IluPrompter.Core.ControllerKind.JogShuttle && source.DeviceId == IluPrompter.App.Services.DemoInput.JogDeviceId;
            Assert.True(wheel || jog, "the demo uses only the preview wheel and the made-up jog source");
        });
        Assert.True(steps.Max(s => s.AtMs) < IluPrompter.App.Services.DemoInput.CycleMs);
        // the jog turns the picture: several clicks, then back
        var jogClicks = steps.Select(s => s.Input).OfType<IluPrompter.Core.RelativeInput>().Where(r => r.ControlId == IluPrompter.Core.ControlIds.JogWheel).ToList();
        Assert.True(jogClicks.Count(c => c.Amount > 0) >= 4);
        // balanced: the wheel and the jog return to where they started and every button that goes down comes up
        foreach (var id in new[] { IluPrompter.Core.ControlIds.Wheel, IluPrompter.Core.ControlIds.JogWheel })
            Assert.Equal(0, steps.Select(s => s.Input).OfType<IluPrompter.Core.RelativeInput>().Where(r => r.ControlId == id).Sum(r => r.Amount));
        foreach (var id in steps.Select(s => s.Input).OfType<IluPrompter.Core.ButtonInput>().Select(b => b.ControlId).Distinct())
        {
            var edges = steps.Select(s => s.Input).OfType<IluPrompter.Core.ButtonInput>().Where(b => b.ControlId == id).ToList();
            Assert.Equal(edges.Count(b => b.IsPressed), edges.Count(b => !b.IsPressed));
        }
    }
}
