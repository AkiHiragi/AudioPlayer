using System;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace AudioPlayer.Services;

public class TrayService
{
    private TaskbarIcon trayIcon;
    private Window      mainWindow;

    public event EventHandler ShowRequested;
    public event EventHandler HideRequested;
    public event EventHandler ExitRequested;
    public event EventHandler PlayPauseRequested;
    public event EventHandler NextTrackRequested;
    public event EventHandler PreviousTrackRequested;

    public TrayService(Window window)
    {
        mainWindow = window;
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        trayIcon = new TaskbarIcon
        {
            ToolTipText = "Audio Player",
            Visibility  = Visibility.Hidden,
            Icon        = System.Drawing.SystemIcons.Application
        };

        var contextMenu = new ContextMenu();

        var playPauseItem = new MenuItem { Header = "Воспроизведение/Пауза" };
        playPauseItem.Click += (s, e) => PlayPauseRequested?.Invoke(this, EventArgs.Empty);

        var previousItem = new MenuItem { Header = "Предыдущий трек" };
        previousItem.Click += (s, e) => PreviousTrackRequested?.Invoke(this, EventArgs.Empty);

        var nextItem = new MenuItem { Header = "Следующий трек" };
        nextItem.Click += (s, e) => NextTrackRequested?.Invoke(this, EventArgs.Empty);

        var showItem = new MenuItem { Header = "Показать" };
        showItem.Click += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new MenuItem { Header = "Выход" };
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        contextMenu.Items.Add(playPauseItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(previousItem);
        contextMenu.Items.Add(nextItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(exitItem);

        trayIcon.ContextMenu = contextMenu;

        trayIcon.TrayMouseDoubleClick += (s, e) =>
        {
            if (mainWindow.Visibility == Visibility.Visible)
                HideRequested?.Invoke(this, EventArgs.Empty);
            else
                ShowRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public void ShowTrayIcon()                    => trayIcon.Visibility = Visibility.Visible;
    public void HideTrayIcon()                    => trayIcon.Visibility = Visibility.Hidden;
    public void UpdateTrackInfo(string trackName) => trayIcon.ToolTipText = $"Audio Player - {trackName}";

    public void ShowBalloonTip(string title, string message)
        => trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);

    public void Dispose()
    {
        trayIcon?.Dispose();
    }
}