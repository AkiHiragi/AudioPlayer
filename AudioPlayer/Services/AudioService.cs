using System.Windows.Media;
using System.Windows.Threading;

namespace AudioPlayer.Services;

public class AudioService
{
    private MediaPlayer     mediaPlayer;
    private DispatcherTimer timer;
    private bool            isPlaying;

    // События для уведомления UI
    public event EventHandler<TimeSpan> PositionChanged;
    public event EventHandler<TimeSpan> DurationChanged;
    public event EventHandler<bool>     PlaybackStateChanged;
    public event EventHandler           TrackEnded;
    public event EventHandler           TrackOpened;

    public bool     IsPlaying => isPlaying;
    public TimeSpan Position  => mediaPlayer?.Position                 ?? TimeSpan.Zero;
    public TimeSpan Duration  => mediaPlayer?.NaturalDuration.TimeSpan ?? TimeSpan.Zero;

    public AudioService()
    {
        InitializePlayer();
    }

    private void InitializePlayer()
    {
        mediaPlayer             =  new MediaPlayer();
        mediaPlayer.MediaEnded  += OnMediaEnded;
        mediaPlayer.MediaOpened += OnMediaOpened;

        timer          =  new DispatcherTimer();
        timer.Interval =  TimeSpan.FromSeconds(1);
        timer.Tick     += OnTimerTick;
    }

    public void LoadTrack(string fileName)
    {
        try
        {
            mediaPlayer.Open(new Uri(fileName));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки трека: {ex.Message}");
        }
    }

    public void Play()
    {
        mediaPlayer.Play();
        isPlaying = true;
        timer.Start();
        PlaybackStateChanged?.Invoke(this, true);
    }

    public void Pause()
    {
        mediaPlayer.Pause();
        isPlaying = false;
        timer.Stop();
        PlaybackStateChanged?.Invoke(this, false);
        System.Diagnostics.Debug.WriteLine("AudioService: Paused");
    }

    public void Stop()
    {
        mediaPlayer.Stop();
        isPlaying = false;
        timer.Stop();
        PlaybackStateChanged?.Invoke(this, false);
    }

    public void SetVolume(double volume)
    {
        if (mediaPlayer != null)
            mediaPlayer.Volume = volume / 100.0;
    }

    public void SetPosition(TimeSpan position)
    {
        if (mediaPlayer != null)
            mediaPlayer.Position = position;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (mediaPlayer.NaturalDuration.HasTimeSpan)
        {
            PositionChanged?.Invoke(this, mediaPlayer.Position);
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (mediaPlayer.NaturalDuration.HasTimeSpan)
        {
            DurationChanged?.Invoke(this, mediaPlayer.NaturalDuration.TimeSpan);
        }

        TrackOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        timer?.Stop();
        mediaPlayer?.Close();
    }
}