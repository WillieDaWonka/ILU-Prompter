using System.Windows.Input;
using IluPrompter.App.Controls;
using IluPrompter.App.Logic;
using IluPrompter.App.ViewModels;
using IluPrompter.App.Views;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>The live panel (DESIGN.md D7): tabs, hide, where the body goes, what is remembered, and the keys.</summary>
public class LivePanelTests
{
    private sealed class Rig
    {
        public FakeSettings Settings { get; } = new();
        public KeyModeState KeyMode { get; } = new();
        public FakeRouter Router { get; } = new();
        public TestClock Clock { get; } = new();
        public LivePanelViewModel Panel { get; private set; }

        public Rig(Func<PrompterSettings, PrompterSettings>? seed = null)
        {
            if (seed is not null) Settings.Update(seed);
            Panel = Create();
        }

        public LivePanelViewModel Create()
        {
            var ui = new ImmediateUi();
            var output = new FakeOutput();
            var margins = new MarginEditorViewModel(Settings, output, ui);
            var layout = new OutputLayoutViewModel(Settings, ui, margins);
            var controller = new ControllerPanelViewModel(Router, Settings, new FakeEngine(), output, ui, Clock.Now);
            return Panel = new LivePanelViewModel(Settings, KeyMode, ui, layout, controller);
        }

        public WindowSettings Window => Settings.Current.ControlWindow;

        /// <summary>A column tall enough for the body to sit inline.</summary>
        public void Tall() => Panel.UpdateFit(230, true);

        /// <summary>A column too short: the bar stays and the body becomes a flyout.</summary>
        public void Short() => Panel.UpdateFit(92, false);
    }

    // ===== defaults and what is remembered =====

    [Fact]
    public void FirstRun_IsTheControllerTab_WithThePanelOpen()
    {
        var rig = new Rig();
        Assert.Equal(LivePanelTab.Controller, rig.Panel.Tab);
        Assert.True(rig.Panel.IsOpen);
        Assert.True(rig.Panel.BodyVisible);
        Assert.Equal("open", rig.Panel.ControllerTabTag);
        Assert.Equal("", rig.Panel.OutputLayoutTabTag);
        Assert.True(rig.Panel.DrawingOnScreen);
        Assert.False(rig.Panel.ShowBarText);
    }

    [Fact]
    public void TheTab_AndTheHiddenFlag_ArePersisted_AndSurviveARestart()
    {
        var rig = new Rig();
        rig.Panel.ShowOutputLayoutTab();
        rig.Panel.ToggleOpen();
        Assert.Equal(LivePanelTab.OutputLayout, rig.Window.LivePanelTab);
        Assert.False(rig.Window.LivePanelOpen);

        var restarted = rig.Create();   // a new view model over the same (reloaded) settings
        Assert.Equal(LivePanelTab.OutputLayout, restarted.Tab);
        Assert.False(restarted.IsOpen);
        Assert.False(restarted.BodyVisible);
    }

    [Fact]
    public void AnOldSettingsFile_OpensOnTheControllerTab()
    {
        var rig = new Rig(s => s with { ControlWindow = new WindowSettings { Width = 1500 } });
        Assert.Equal(LivePanelTab.Controller, rig.Panel.Tab);
        Assert.True(rig.Panel.IsOpen);
    }

    [Fact]
    public void SavingTheWindowBounds_KeepsTheLivePanelSettings()
    {
        var current = new WindowSettings { LivePanelTab = LivePanelTab.OutputLayout, LivePanelOpen = false };
        var saved = WindowBounds.Apply(current, 10, 20, 1500, 900, maximised: false, boundsValid: true);
        Assert.Equal(LivePanelTab.OutputLayout, saved.LivePanelTab);
        Assert.False(saved.LivePanelOpen);
        Assert.Equal(1500, saved.Width);

        var maximisedOnly = WindowBounds.Apply(current, double.PositiveInfinity, 0, -1, -1, maximised: true, boundsValid: false);
        Assert.Equal(LivePanelTab.OutputLayout, maximisedOnly.LivePanelTab);
        Assert.False(maximisedOnly.LivePanelOpen);
        Assert.True(maximisedOnly.IsMaximized);
        Assert.Equal(current.Width, maximisedOnly.Width);
    }

    // ===== tabs =====

    [Fact]
    public void ClickingTheOpenTab_DoesNothing_ItNeverHidesThePanel()
    {
        var rig = new Rig();
        var updates = rig.Settings.UpdateCount;
        rig.Panel.ShowControllerTab();
        Assert.True(rig.Panel.IsOpen);
        Assert.Equal(updates, rig.Settings.UpdateCount);   // not even a write
    }

    [Fact]
    public void ClickingTheOtherTab_SwitchesTabs_AndRemembersIt()
    {
        var rig = new Rig();
        rig.Panel.ShowOutputLayoutTab();
        Assert.True(rig.Panel.IsOutputLayoutTab);
        Assert.Equal("open", rig.Panel.OutputLayoutTabTag);
        Assert.Equal("", rig.Panel.ControllerTabTag);
        Assert.False(rig.Panel.DrawingOnScreen);
        Assert.True(rig.Panel.ShowBarText);   // the last-input line sits in the bar while the drawing is off screen
        Assert.Equal(LivePanelTab.OutputLayout, rig.Window.LivePanelTab);
    }

    [Fact]
    public void ClickingATabWhileHidden_OpensThePanelOnThatTab()
    {
        var rig = new Rig();
        rig.Panel.ToggleOpen();
        Assert.False(rig.Panel.IsOpen);
        rig.Panel.ShowOutputLayoutTab();
        Assert.True(rig.Panel.IsOpen);
        Assert.Equal(LivePanelTab.OutputLayout, rig.Panel.Tab);

        rig.Panel.ToggleOpen();
        rig.Panel.ShowOutputLayoutTab();   // the tab it was already on, but hidden: it opens
        Assert.True(rig.Panel.IsOpen);
        Assert.Equal(LivePanelTab.OutputLayout, rig.Panel.Tab);
    }

    [Fact]
    public void WhileHidden_NeitherTabHasTheBar_AndTheToggleOffersToShow()
    {
        var rig = new Rig();
        Assert.Equal("Hide this panel (F9)", rig.Panel.ToggleTooltip);
        Assert.Equal(IluPrompter.Theme.IluIcons.ChevronDown, rig.Panel.ToggleGlyph);
        rig.Panel.ToggleOpen();
        Assert.Equal("", rig.Panel.ControllerTabTag);
        Assert.Equal("", rig.Panel.OutputLayoutTabTag);
        Assert.Equal("Show this panel (F9)", rig.Panel.ToggleTooltip);
        Assert.Equal("Show this panel", rig.Panel.ToggleName);
        Assert.Equal(IluPrompter.Theme.IluIcons.Up, rig.Panel.ToggleGlyph);
        Assert.True(rig.Panel.ShowBarText);
    }

    [Fact]
    public void CtrlF9_OpensTheOtherTab_ShowingThePanelIfItIsHidden()
    {
        var rig = new Rig();
        rig.Panel.OpenOtherTab();
        Assert.Equal(LivePanelTab.OutputLayout, rig.Panel.Tab);
        Assert.True(rig.Panel.IsOpen);

        rig.Panel.OpenOtherTab();
        Assert.Equal(LivePanelTab.Controller, rig.Panel.Tab);

        rig.Panel.ToggleOpen();
        rig.Panel.OpenOtherTab();
        Assert.True(rig.Panel.IsOpen);
        Assert.Equal(LivePanelTab.OutputLayout, rig.Panel.Tab);
    }

    [Fact]
    public void HidingThePanel_FreesNothing_TheFitDoesNotChange()
    {
        var rig = new Rig();
        rig.Tall();
        var form = rig.Panel.Form;
        rig.Panel.ToggleOpen();
        Assert.Equal(form, rig.Panel.Form);   // the preview and the transport do not move: the space stays empty
        Assert.False(rig.Panel.InlineBodyVisible);
    }

    // ===== fit by available height =====

    [Theory]
    [InlineData(400, LivePanelBodyForm.InlineLarge)]
    [InlineData(230, LivePanelBodyForm.InlineLarge)]
    [InlineData(212, LivePanelBodyForm.InlineLarge)]
    [InlineData(209, LivePanelBodyForm.InlineLarge)]
    [InlineData(208, LivePanelBodyForm.InlineSmall)]
    [InlineData(161, LivePanelBodyForm.InlineSmall)]
    [InlineData(160, LivePanelBodyForm.Flyout)]
    [InlineData(92, LivePanelBodyForm.Flyout)]
    [InlineData(40, LivePanelBodyForm.Flyout)]
    [InlineData(0, LivePanelBodyForm.Flyout)]
    public void TheBodyGoesWhereTheHeightAllows(double free, LivePanelBodyForm form) =>
        Assert.Equal(form, LivePanelFit.ForHeight(free, true).Form);

    [Theory]
    [InlineData(40, true, false, false)]   // fits: nothing gives way
    [InlineData(39, true, true, false)]    // step 1: the transport captions hide (20 px freed)
    [InlineData(20, true, true, false)]    // 20 + 20 = 40: just enough
    [InlineData(19, true, true, true)]     // step 2 as well: the preview's output caption moves into its tooltip
    [InlineData(0, true, true, true)]
    [InlineData(39, false, false, true)]   // Compact and Narrow have no captions to hide: straight to step 2
    [InlineData(100, false, false, false)]
    public void TheBarAlwaysFits_ByTheTwoGiveWays_InOrder(double free, bool captionsShown, bool hideCaptions, bool movePreviewCaption)
    {
        var fit = LivePanelFit.ForHeight(free, captionsShown);
        Assert.Equal(hideCaptions, fit.HideTransportCaptions);
        Assert.Equal(movePreviewCaption, fit.MovePreviewCaption);
    }

    [Fact]
    public void TheFit_FromTheReferenceWindows_MatchesTheDesignEstimates()
    {
        // 1920 x 1080 at 100 % (Wide) inline large; 1920 x 1080 at 125 % (Compact) inline small; 1366 x 768: bar plus flyout.
        Assert.Equal(LivePanelBodyForm.InlineLarge, LivePanelFit.ForHeight(212, true).Form);
        Assert.Equal(LivePanelBodyForm.InlineSmall, LivePanelFit.ForHeight(170, false).Form);
        Assert.Equal(LivePanelBodyForm.Flyout, LivePanelFit.ForHeight(92, false).Form);
    }

    [Fact]
    public void TheFit_RaisesOnlyWhatChanged_AndSizesTheDrawing()
    {
        var rig = new Rig();
        var raised = new List<string?>();
        rig.Panel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        rig.Panel.UpdateFit(190, false);
        Assert.NotEmpty(raised);
        Assert.True(rig.Panel.IsSmall);
        Assert.True(rig.Panel.Controller.Small);

        rig.Panel.UpdateFit(230, true);
        Assert.False(rig.Panel.IsSmall);
        Assert.False(rig.Panel.Controller.Small);

        raised.Clear();
        rig.Panel.UpdateFit(230, true);   // nothing changed: nothing raised
        Assert.Empty(raised);
    }

    [Theory]
    [InlineData(802, 600, true)]
    [InlineData(616, 600, true)]
    [InlineData(615, 599, false)]
    [InlineData(500, 484, false)]
    public void TheFlyout_Is600Wide_OrTheEditorMinus16_AndLargeOnlyWhenItsInnerWidthIs576(double editor, double width, bool large)
    {
        Assert.Equal(width, LivePanelFit.FlyoutWidth(editor));
        Assert.Equal(large, LivePanelFit.FlyoutIsLarge(editor));
        var rig = new Rig();
        rig.Short();
        rig.Panel.UpdateEditorWidth(editor);
        Assert.Equal(!large, rig.Panel.IsSmall);
        Assert.Equal(width, rig.Panel.FlyoutWidth);
    }

    // ===== the flyout =====

    [Fact]
    public void TheFlyout_IsClosedAtLaunch_EvenWhenTheSettingSaysOpen()
    {
        var rig = new Rig();
        Assert.True(rig.Window.LivePanelOpen);
        rig.Short();
        Assert.True(rig.Panel.IsFlyoutForm);
        Assert.False(rig.Panel.IsOpen);
        Assert.False(rig.Panel.FlyoutVisible);
        Assert.False(rig.Panel.InlineBodyVisible);
        Assert.Equal("", rig.Panel.ControllerTabTag);   // closed: no bar under either tab
        Assert.Equal(IluPrompter.Theme.IluIcons.Up, rig.Panel.ToggleGlyph);
    }

    [Fact]
    public void OpeningTheFlyout_AndHidingIt_WriteTheSetting()
    {
        var rig = new Rig(s => s with { ControlWindow = s.ControlWindow with { LivePanelOpen = false } });
        rig.Short();
        rig.Panel.ToggleOpen();
        Assert.True(rig.Panel.FlyoutVisible);
        Assert.True(rig.Window.LivePanelOpen);
        Assert.Equal("open", rig.Panel.ControllerTabTag);

        rig.Panel.ToggleOpen();
        Assert.False(rig.Panel.FlyoutVisible);
        Assert.False(rig.Window.LivePanelOpen);
    }

    [Fact]
    public void ATabOpensTheFlyout_OnThatTab()
    {
        var rig = new Rig();
        rig.Short();
        rig.Panel.ShowOutputLayoutTab();
        Assert.True(rig.Panel.FlyoutVisible);
        Assert.Equal(LivePanelTab.OutputLayout, rig.Panel.Tab);
        Assert.False(rig.Panel.DrawingOnScreen);
    }

    [Fact]
    public void TheFlyout_HidesWhileYouType_AndComesBackWithTheTransportKeys()
    {
        var rig = new Rig();
        rig.Short();
        rig.Panel.ToggleOpen();
        Assert.True(rig.Panel.FlyoutVisible);
        Assert.True(rig.Panel.DrawingOnScreen);

        rig.KeyMode.Mode = KeyMode.Typing;
        Assert.False(rig.Panel.FlyoutVisible);          // never covers the line you are typing
        Assert.False(rig.Panel.DrawingOnScreen);        // so the bar takes the last-input line
        Assert.True(rig.Panel.ShowBarText);
        Assert.False(rig.Panel.Controller.Active);
        Assert.True(rig.Panel.IsOpen);                  // it is only out of the way

        rig.KeyMode.Mode = KeyMode.Transport;
        Assert.True(rig.Panel.FlyoutVisible);
        Assert.True(rig.Panel.Controller.Active);
    }

    [Fact]
    public void AnInlineBody_StaysWhileYouType()
    {
        var rig = new Rig();
        rig.Tall();
        rig.KeyMode.Mode = KeyMode.Typing;
        Assert.True(rig.Panel.InlineBodyVisible);
        Assert.True(rig.Panel.DrawingOnScreen);
    }

    [Fact]
    public void F9_WhileTheFlyoutIsHiddenByTyping_HidesItForGood()
    {
        var rig = new Rig();
        rig.Short();
        rig.Panel.ToggleOpen();
        rig.KeyMode.Mode = KeyMode.Typing;
        rig.Panel.ToggleOpen();
        rig.KeyMode.Mode = KeyMode.Transport;
        Assert.False(rig.Panel.FlyoutVisible);
        Assert.False(rig.Window.LivePanelOpen);
    }

    [Fact]
    public void TheFlyout_ClosesWhenTheWindowChangesSize_ButTheSettingIsKept()
    {
        var rig = new Rig();
        rig.Short();
        rig.Panel.ToggleOpen();
        rig.Panel.OnWindowResized();
        Assert.False(rig.Panel.FlyoutVisible);
        Assert.True(rig.Window.LivePanelOpen);
    }

    [Fact]
    public void GrowingTheWindow_BringsTheInlineBodyBack_ThatFollowsTheSetting()
    {
        var rig = new Rig();
        rig.Short();
        Assert.False(rig.Panel.InlineBodyVisible);
        rig.Panel.UpdateFit(230, true);
        Assert.True(rig.Panel.InlineBodyVisible);
        Assert.False(rig.Panel.IsFlyoutForm);
    }

    [Fact]
    public void ShrinkingTheWindow_DoesNotOpenAFlyoutByItself()
    {
        var rig = new Rig();
        rig.Tall();
        rig.Panel.UpdateFit(92, false);
        Assert.True(rig.Panel.IsFlyoutForm);
        Assert.False(rig.Panel.FlyoutVisible);
    }

    // ===== the poll skips what is not on screen =====

    [Fact]
    public void ThePoll_SkipsTheDrawing_WhenThePanelIsHiddenOrOnTheOtherTab()
    {
        var rig = new Rig();
        rig.Tall();
        rig.Panel.Poll();
        Assert.True(rig.Panel.Controller.Active);
        rig.Panel.ShowOutputLayoutTab();
        rig.Panel.Poll();
        Assert.False(rig.Panel.Controller.Active);
        rig.Panel.ShowControllerTab();
        rig.Panel.ToggleOpen();
        rig.Panel.Poll();
        Assert.False(rig.Panel.Controller.Active);
        rig.Panel.ToggleOpen();
        rig.Panel.Poll();
        Assert.True(rig.Panel.Controller.Active);
    }

    [Fact]
    public void TheBarLine_FollowsTheControllerWhileTheDrawingIsHidden()
    {
        var rig = new Rig(s => s with { Input = s.Input with { SelectedController = ControllerKind.Xbox } });
        var pad = new InputSourceId(ControllerKind.Xbox, "xinput:0");
        rig.Router.RaiseStatus(new DeviceStatus(pad, "Xbox controller", DeviceConnection.Connected));
        rig.Panel.ToggleOpen();
        rig.Router.RaiseInput(new ButtonInput(pad, ControlIds.A, 0, true));
        rig.Clock.Advance(100);
        rig.Panel.Poll();
        Assert.Equal("Last: A · Play / pause", rig.Panel.BarText);
    }

    // ===== keys =====

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.Tab)]
    [InlineData(Key.B)]
    [InlineData(Key.M)]
    [InlineData(Key.D0)]
    [InlineData(Key.Escape)]
    public void TheTabBar_IgnoresTheTransportKeys(Key key) => Assert.False(PanelTabButton.HandlesKey(key));

    [Fact]
    public void TheTabBar_AnswersEnter() => Assert.True(PanelTabButton.HandlesKey(Key.Enter));

    [WpfFact]
    public void ATabButton_DoesNotMarkTheTransportKeysHandled_AndDoesNotClickOnSpace()
    {
        var button = new PanelTabButton { Content = "Controller" };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("tab-test") { Width = 10, Height = 10 });
        source.RootVisual = button;
        foreach (var key in new[] { Key.Space, Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.PageUp, Key.PageDown })
        {
            var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
            button.RaiseEvent(down);
            Assert.False(down.Handled, key + " must carry on to the transport");
            var up = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyUpEvent };
            button.RaiseEvent(up);
            Assert.False(up.Handled, key + " must carry on to the transport");
        }

        Assert.Equal(0, clicks);

        var enter = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
        button.RaiseEvent(enter);
        Assert.Equal(1, clicks);   // Enter presses it, as the design says
    }

    [Fact]
    public void F9AndCtrlF9_AreWindowCommands_NotHotKeys()
    {
        var f9 = Assert.IsType<KeyGesture>(Assert.Single(AppCommands.ToggleLivePanel.InputGestures));
        Assert.Equal(Key.F9, f9.Key);
        Assert.Equal(ModifierKeys.None, f9.Modifiers);
        var ctrlF9 = Assert.IsType<KeyGesture>(Assert.Single(AppCommands.OtherLivePanelTab.InputGestures));
        Assert.Equal(Key.F9, ctrlF9.Key);
        Assert.Equal(ModifierKeys.Control, ctrlF9.Modifiers);
    }

    [Fact]
    public void TheShortcutList_NamesF9AndCtrlF9()
    {
        var all = ShortcutCatalog.Groups.SelectMany(g => g.Entries).Select(e => e.Keys).ToList();
        Assert.Contains("F9", all);
        Assert.Contains("Ctrl+F9", all);
    }

    [Fact]
    public void F9AndCtrlF9_AreNotRouterBindings_SoNothingElseTakesThem()
    {
        var all = DefaultBindings.KeyboardGlobal.Keys.Concat(DefaultBindings.KeyboardTransport.Keys).ToList();
        Assert.DoesNotContain("F9", all);
        Assert.DoesNotContain("Ctrl+F9", all);
    }
}
