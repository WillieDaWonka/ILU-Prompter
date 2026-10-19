using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.Core;

namespace IluPrompter.Input.Tests;

public class AppInputBridgeTests
{
    [Theory]
    [InlineData(120, 1.0)]
    [InlineData(-120, -1.0)]
    [InlineData(240, 2.0)]
    [InlineData(30, 0.25)]
    [InlineData(-15, -0.125)]
    [InlineData(1, 1.0 / 120.0)]
    public void WheelNotches_is_delta_over_120_without_rounding(int delta, double expected) =>
        Assert.Equal(expected, AppInputBridge.WheelNotches(delta));

    [Fact]
    public void Wheel_event_uses_app_mouse_source_and_Wheel_control()
    {
        var e = AppInputBridge.CreateWheelEvent(60, 42);
        Assert.Equal(new InputSourceId(ControllerKind.MouseWheel, AppSources.App), e.Source);
        Assert.Equal(ControlIds.Wheel, e.ControlId);
        Assert.Equal(0.5, e.Amount);
        Assert.Equal(42, e.Timestamp);
    }

    [Theory]
    [InlineData(MouseButton.Middle, ControlIds.MiddleButton)]
    [InlineData(MouseButton.XButton1, ControlIds.XButton1)]
    [InlineData(MouseButton.XButton2, ControlIds.XButton2)]
    public void Mouse_buttons_map_to_canonical_ids_with_both_edges(MouseButton button, string id)
    {
        var down = AppInputBridge.CreateMouseButtonEvent(button, true, 1);
        var up = AppInputBridge.CreateMouseButtonEvent(button, false, 2);
        Assert.NotNull(down);
        Assert.NotNull(up);
        Assert.Equal(id, down!.ControlId);
        Assert.True(down.IsPressed);
        Assert.False(up!.IsPressed);
        Assert.Equal(ControllerKind.MouseWheel, down.Source.Kind);
        Assert.Equal(AppSources.App, down.Source.DeviceId);
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void Left_and_right_buttons_are_not_forwarded(MouseButton button) =>
        Assert.Null(AppInputBridge.CreateMouseButtonEvent(button, true, 1));

    [Fact]
    public void Key_event_source_depends_on_text_input_context()
    {
        var transport = AppInputBridge.CreateKeyEvent("Space", inTextInput: false, isPressed: true, isRepeat: false, 5);
        var typing = AppInputBridge.CreateKeyEvent("Space", inTextInput: true, isPressed: true, isRepeat: true, 5);

        Assert.Equal(new InputSourceId(ControllerKind.Keyboard, AppSources.App), transport.Source);
        Assert.Equal(new InputSourceId(ControllerKind.Keyboard, AppSources.TextInput), typing.Source);
        Assert.False(transport.IsRepeat);
        Assert.True(typing.IsRepeat);
        Assert.Equal("Space", transport.ControlId);
    }

    [Fact]
    public void Preview_wheel_event_is_forwarded_unrounded_and_Handled_follows_the_sink()
    {
        StaTestHelper.Run(() =>
        {
            var sink = new RecordingSink { HandleAll = true };
            var bridge = new AppInputBridge(sink);
            var area = new Border();
            bridge.AttachWheelArea(area);

            var args = RaiseWheel(area, 30);
            var rel = Assert.IsType<RelativeInput>(Assert.Single(sink.Inputs));
            Assert.Equal(ControlIds.Wheel, rel.ControlId);
            Assert.Equal(0.25, rel.Amount);          // 30 / 120, not rounded to 0
            Assert.True(args.Handled);

            sink.HandleAll = false;
            args = RaiseWheel(area, -240);
            Assert.Equal(-2.0, ((RelativeInput)sink.Inputs[1]).Amount);
            Assert.False(args.Handled);

            bridge.DetachWheelArea(area);
            RaiseWheel(area, 120);
            Assert.Equal(2, sink.Inputs.Count);
        });
    }

    [Fact]
    public void Attaching_the_same_element_twice_does_not_double_submit()
    {
        StaTestHelper.Run(() =>
        {
            var sink = new RecordingSink();
            var bridge = new AppInputBridge(sink);
            var area = new Border();
            bridge.AttachWheelArea(area);
            bridge.AttachWheelArea(area);

            RaiseWheel(area, 120);
            Assert.Single(sink.Inputs);
        });
    }

    [Fact]
    public void Middle_button_edges_are_forwarded_and_release_follows_handled_press()
    {
        StaTestHelper.Run(() =>
        {
            var sink = new RecordingSink { HandleAll = true };
            var bridge = new AppInputBridge(sink);
            var area = new Border();
            bridge.AttachWheelArea(area);

            var down = RaiseButton(area, MouseButton.Middle, UIElement.PreviewMouseDownEvent);
            var up = RaiseButton(area, MouseButton.Middle, UIElement.PreviewMouseUpEvent);

            Assert.Equal(2, sink.Inputs.Count);
            Assert.True(((ButtonInput)sink.Inputs[0]).IsPressed);
            Assert.False(((ButtonInput)sink.Inputs[1]).IsPressed);
            Assert.True(down.Handled);
            Assert.True(up.Handled);

            // A left click is not forwarded at all.
            RaiseButton(area, MouseButton.Left, UIElement.PreviewMouseDownEvent);
            Assert.Equal(2, sink.Inputs.Count);
        });
    }

    [Fact]
    public void Typing_context_is_text_boxes_password_boxes_and_editable_combos_only()
    {
        StaTestHelper.Run(() =>
        {
            Assert.True(TypingContext.IsTextInput(new TextBox()));
            Assert.True(TypingContext.IsTextInput(new RichTextBox()));
            Assert.True(TypingContext.IsTextInput(new PasswordBox()));
            Assert.True(TypingContext.IsTextInput(new ComboBox { IsEditable = true }));

            Assert.False(TypingContext.IsTextInput(new ComboBox { IsEditable = false }));
            Assert.False(TypingContext.IsTextInput(new Button()));
            Assert.False(TypingContext.IsTextInput(new Border()));
            Assert.False(TypingContext.IsTextInput(null));
        });
    }

    private static MouseWheelEventArgs RaiseWheel(UIElement target, int delta)
    {
        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
        };
        target.RaiseEvent(args);
        return args;
    }

    private static MouseButtonEventArgs RaiseButton(UIElement target, MouseButton button, RoutedEvent routedEvent)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
        {
            RoutedEvent = routedEvent,
        };
        target.RaiseEvent(args);
        return args;
    }
}
