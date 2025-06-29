using System;
using System.Windows;
using System.Windows.Threading;

namespace AudioPlayer.Services;

public class NotificationService
{
    public event EventHandler<string> TitleChangeRequested;
    
    public void ShowNotification(string message)
    {
        TitleChangeRequested?.Invoke(this, $"✅ {message}");

        var notificationTimer = new DispatcherTimer();
        notificationTimer.Interval = TimeSpan.FromSeconds(2);
        notificationTimer.Tick += (s, e) =>
        {
            TitleChangeRequested?.Invoke(this, "Audio Player");
            notificationTimer.Stop();
        };
        notificationTimer.Start();
    }
    
    public void ShowTrackTitle(string trackName)
    {
        TitleChangeRequested?.Invoke(this, $"Audio Player - {trackName}");
    }
    
    public void ShowDefaultTitle()
    {
        TitleChangeRequested?.Invoke(this, "Audio Player");
    }
}