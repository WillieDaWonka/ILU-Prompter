using System.Diagnostics;
using IluPrompter.Core;

namespace IluPrompter.App.Services;

/// <summary>One step of the demo: an input event this long after the cycle started.</summary>
public sealed record DemoStep(int AtMs, InputEvent Input);

/// <summary>
/// Dev only (<c>--demo-input</c>, never the default): feeds a scripted sequence of fake wheel and button events through the
/// same path a real input takes, <see cref="IInputSink.Submit"/> from a background thread, so the live panel's controller
/// drawing can be seen and screenshotted without any hardware. It only ever uses the mouse wheel over the preview
/// (<see cref="AppSources.App"/>), plus a few jog-wheel clicks from a made-up Contour source (<see cref="Jog"/>, which is not a
/// device) so the jog's picture visibly turns. No Xbox, no dedicated mouse, no Raw Input and no device is ever started or read.
/// The router counts the jog clicks only when Jog-shuttle is the chosen controller (as for a real one), which starts that
/// controller's own provider, so leave them be on a machine where that is not wanted: with the default mouse wheel they are ignored.
/// The wheel, jog and Middle events act like real ones (speed nudges, lines, play / pause), balanced so a cycle leaves the speed,
/// the position and the transport where they were.
/// </summary>
public static class DemoInput
{
    public const int CycleMs = 15_000;

    private static readonly InputSourceId Preview = new(ControllerKind.MouseWheel, AppSources.App);

    /// <summary>A made-up jog-shuttle source: not a device, never opened. Only the router's kind check sees it.</summary>
    public const string JogDeviceId = "demo-input:jog";

    private static readonly InputSourceId Jog = new(ControllerKind.JogShuttle, JogDeviceId);

    /// <summary>The script for one cycle, with <paramref name="timestamp"/> on every event.</summary>
    public static IReadOnlyList<DemoStep> Script(long timestamp)
    {
        var steps = new List<DemoStep>();
        void Wheel(int at, double amount) => steps.Add(new DemoStep(at, new RelativeInput(Preview, ControlIds.Wheel, timestamp, amount)));
        void Click(int at, double amount) => steps.Add(new DemoStep(at, new RelativeInput(Jog, ControlIds.JogWheel, timestamp, amount)));
        void Button(int at, string id, bool down) => steps.Add(new DemoStep(at, new ButtonInput(Preview, id, timestamp, down)));

        // 1.0 s: three notches away from you (the burst reads "▲ 3" for the next two seconds)
        Wheel(1000, 1);
        Wheel(1200, 1);
        Wheel(1400, 1);
        // 4.0 s: the side button (unbound: the neutral flash), held 2.5 s
        Button(4000, ControlIds.XButton1, true);
        Button(6500, ControlIds.XButton1, false);
        // 5.0 s: Middle (bound: Play / pause) held 2.5 s; the script starts moving
        Button(5000, ControlIds.MiddleButton, true);
        Button(7500, ControlIds.MiddleButton, false);
        // 9.0 s: Middle again, to pause
        Button(9000, ControlIds.MiddleButton, true);
        Button(9400, ControlIds.MiddleButton, false);
        // 11.0 s: three notches towards you, which undoes the first three
        Wheel(11000, -1);
        Wheel(11200, -1);
        Wheel(11400, -1);
        // 2.0 s: the jog wheel turns six clicks one way (the picture turns 216 degrees), and 12.0 s the same back
        for (var i = 0; i < 6; i++) Click(2000 + (i * 250), 1);
        for (var i = 0; i < 6; i++) Click(12000 + (i * 250), -1);
        return steps.OrderBy(s => s.AtMs).ToList();
    }

    /// <summary>Run the script in a loop on a background thread until <paramref name="cancel"/>.</summary>
    public static Task Start(IInputSink sink, IAppLog log, CancellationToken cancel)
    {
        log.Warn("app", "Dev flag --demo-input: a scripted sequence of fake mouse wheel and jog events is being fed to the input router. Not for shows.");
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, cancel).ConfigureAwait(false);   // let the window settle
                while (!cancel.IsCancellationRequested)
                {
                    var started = Environment.TickCount64;
                    foreach (var step in Script(Stopwatch.GetTimestamp()))
                    {
                        var wait = started + step.AtMs - Environment.TickCount64;
                        if (wait > 0) await Task.Delay((int)wait, cancel).ConfigureAwait(false);
                        sink.Submit(step.Input with { Timestamp = Stopwatch.GetTimestamp() });
                    }

                    var rest = started + CycleMs - Environment.TickCount64;
                    if (rest > 0) await Task.Delay((int)rest, cancel).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Error("app", "The demo input stopped", ex);
            }
        }, cancel);
    }
}
