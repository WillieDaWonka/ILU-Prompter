using IluPrompter.Core;

namespace IluPrompter.App.Services;

/// <summary>
/// Show, hide and blank the output from the control window (button, menu, F11). Remembers whether you have pressed
/// Show output yet, because the output chip warns about a missing second display only after you have asked for it.
/// Closing the output while LIVE asks first (DESIGN.md: "Close the output?"), with the safe choice as the default.
/// </summary>
public sealed class OutputController
{
    private readonly IOutputSurface _output;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private bool _userPressedShow;

    public OutputController(IOutputSurface output, IDialogService dialogs, IAppLog log)
    {
        _output = output;
        _dialogs = dialogs;
        _log = log;
    }

    public bool UserPressedShow
    {
        get => _userPressedShow;
        private set
        {
            if (_userPressedShow == value) return;
            _userPressedShow = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised on the UI thread when <see cref="UserPressedShow"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>F11 and the Show output button. Call on the UI thread.</summary>
    public async Task ToggleAsync()
    {
        try
        {
            var state = _output.State;
            if (!state.IsOpen)
            {
                UserPressedShow = true;
                await _output.ShowAsync();
                return;
            }

            if (state.IsLive)
            {
                var choice = _dialogs.Ask(new DialogRequest(
                    "Close the output?",
                    "The presenter's screen goes to the Windows desktop.",
                    "Close output",
                    "Keep output",
                    PrimaryIsDefault: false));
                if (choice != DialogChoice.Primary) return;
            }

            UserPressedShow = false;
            await _output.HideAsync();
        }
        catch (Exception ex)
        {
            _log.Error("output", "Show or hide failed", ex);
        }
    }

    public void ToggleBlank() => _output.SetBlank(!_output.State.IsBlank);

    public void SetBlank(bool blank)
    {
        if (_output.State.IsBlank != blank) _output.SetBlank(blank);
    }
}
