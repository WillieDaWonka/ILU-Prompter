using System.Diagnostics;
using IluPrompter.Core;

namespace IluPrompter.App.Services;

/// <summary>One step of the demo: an input event this long after the cycle started.</summary>
public sealed record DemoStep(int AtMs, InputEvent Input);

/// <summary>
/// Dev only (<c>--demo-input</c>, never the default): feeds a scripted sequence of fake wheel and button events through the
/// same path a real input takes, <see cref="IInputSink.Submit"/> from a background thread, so the live panel's controller
/// drawing can be seen and screenshotted without any hardware. It only ever uses the mouse wheel over the preview
/// (<see cref="AppSources.App"/>): no Xbox, no Contour, no dedicated mouse, no Raw Input and no device is ever started or read.
/// The wheel and Middle events act like real ones (speed nudges, play / pause), balanced so a cycle leaves the speed and the
/// transport where they were.
/// </summary>
public static class DemoInput
{
    public const int CycleMs = 15_000;

    private static readonly InputSourceId Preview = new(ControllerKind.MouseWheel, AppSources.App);

    /// <summary>The script for one cycle, with <paramref name="timestamp"/> on every event.</summary>
    public static IReadOnlyList<DemoStep> Script(long timestamp)
    {
        var steps = new List<DemoStep>();
        void Wheel(int at, double amount) => steps.Add(new DemoStep(at, new RelativeInput(Preview, ControlIds.Wheel, timestamp, amount)));
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
        return steps.OrderBy(s => s.AtMs).ToList();
    }

    /// <summary>Run the script in a loop on a background thread until <paramref name="cancel"/>.</summary>
    public static Task Start(IInputSink sink, IAppLog log, CancellationToken cancel)
    {
        log.Warn("app", "Dev flag --demo-input: a scripted sequence of fake mouse wheel events is being fed to the input router. Not for shows.");
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
