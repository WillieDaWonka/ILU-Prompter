using System.Windows;
using System.Windows.Threading;

namespace IluPrompter.App.Services;

/// <summary>
/// Marshals work to the control UI thread. View models take this instead of touching Dispatcher, so they can be
/// tested without WPF. Events from the output thread and the provider threads always go through <see cref="Post"/>.
/// </summary>
public interface IUiThread
{
    bool CheckAccess();

    /// <summary>Run on the UI thread: now if already there, otherwise queued.</summary>
    void Post(Action action);
}

public sealed class WpfUiThread : IUiThread
{
    private Dispatcher Dispatcher => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    public bool CheckAccess() => Dispatcher.CheckAccess();

    public void Post(Action action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }
}
