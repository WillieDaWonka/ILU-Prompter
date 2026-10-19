using IluPrompter.Core;

namespace IluPrompter.Input.Tests;

/// <summary>Runs an action on a fresh STA thread (WPF elements need one). Touches no devices.</summary>
internal static class StaTestHelper
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("STA test thread did not finish.");
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

/// <summary>Records everything submitted; Submit returns <see cref="HandleAll"/>.</summary>
internal sealed class RecordingSink : IInputSink
{
    public List<InputEvent> Inputs { get; } = new();

    public List<DeviceStatus> Statuses { get; } = new();

    public bool HandleAll { get; set; } = true;

    public bool Submit(InputEvent input)
    {
        lock (Inputs)
        {
            Inputs.Add(input);
        }

        return HandleAll;
    }

    public void ReportStatus(DeviceStatus status)
    {
        lock (Statuses)
        {
            Statuses.Add(status);
        }
    }
}
