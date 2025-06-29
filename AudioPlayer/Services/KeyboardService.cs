using System.Windows.Input;

namespace AudioPlayer.Services;

public class KeyboardService
{
    private          DateTime lastLeftKeyPress    = DateTime.MinValue;
    private          DateTime lastRightKeyPress   = DateTime.MinValue;
    private readonly TimeSpan doubleClickInterval = TimeSpan.FromMilliseconds(400);

    // События для различных команд
    public event EventHandler         StopRequested;
    public event EventHandler         PlayPauseRequested;
    public event EventHandler         NextTrackRequested;
    public event EventHandler         PreviousTrackRequested;
    public event EventHandler<int>    SeekRequested;
    public event EventHandler<double> VolumeChangeRequested;
    public event EventHandler         MuteToggleRequested;
    public event EventHandler         RepeatModeRequested;
    public event EventHandler         ShuffleToggleRequested;
    public event EventHandler         OpenFileRequested;
    public event EventHandler         FullscreenToggleRequested;

    public void HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Space:
                PlayPauseRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.S:
                StopRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.M:
                MuteToggleRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.Up:
                VolumeChangeRequested?.Invoke(this, 10);
                break;

            case Key.Down:
                VolumeChangeRequested?.Invoke(this, -10);
                break;

            case Key.Left:
                HandleLeftKey();
                break;

            case Key.Right:
                HandleRightKey();
                break;

            case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                OpenFileRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.F:
                FullscreenToggleRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.R:
                RepeatModeRequested?.Invoke(this, EventArgs.Empty);
                break;

            case Key.H:
                ShuffleToggleRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void HandleLeftKey()
    {
        var now = DateTime.Now;
        if (now - lastLeftKeyPress < doubleClickInterval)
        {
            PreviousTrackRequested?.Invoke(this, EventArgs.Empty);
            lastLeftKeyPress = DateTime.MinValue;
        }
        else
        {
            SeekRequested?.Invoke(this, -10);
            lastLeftKeyPress = now;
        }
    }

    private void HandleRightKey()
    {
        var now = DateTime.Now;
        if (now - lastRightKeyPress < doubleClickInterval)
        {
            NextTrackRequested?.Invoke(this, EventArgs.Empty);
            lastRightKeyPress = DateTime.MinValue;
        }
        else
        {
            SeekRequested?.Invoke(this, 10);
            lastRightKeyPress = now;
        }
    }
}