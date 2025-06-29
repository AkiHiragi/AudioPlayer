using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using AudioPlayer.Models;
using AudioPlayer.Services;
using Microsoft.Win32;
using ContextMenuService = AudioPlayer.Services.ContextMenuService;

namespace AudioPlayer
{
    public partial class MainWindow : Window
    {
        // Сервисы
        private AudioService         audioService;
        private PlayListService      playlistService;
        private VisualizationService visualizationService;
        private KeyboardService      keyboardService;
        private PlaylistUIService    playlistUIService;
        private DragDropService      dragDropService;
        private ContextMenuService   contextMenuService;
        private NotificationService  notificationService;

        // UI состояние
        private bool            isUserDragging = false;
        private DispatcherTimer autoSaveTimer;

        // Auto-save fields
        private readonly TimeSpan autoSaveInterval = TimeSpan.FromMinutes(2);

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            SetupEventHandlers();

            playlistUIService.LoadAutoSavedPlaylist();
            StartAutoSaveTimer();

            this.Loaded += (s, e) => this.Focus();
        }

        private void InitializeServices()
        {
            audioService         = new AudioService();
            playlistService      = new PlayListService();
            visualizationService = new VisualizationService(VisualizationCanvas);
            keyboardService      = new KeyboardService();
            playlistUIService    = new PlaylistUIService(playlistService, audioService);
            dragDropService      = new DragDropService(playlistService);
            contextMenuService   = new ContextMenuService(playlistService, audioService);
            notificationService  = new NotificationService();

            CurrentTime.Text = "00:00";
            TotalTime.Text   = "00:00";
        }

        private void SetupEventHandlers()
        {
            // AudioService Events
            audioService.PositionChanged      += OnPositionChanged;
            audioService.DurationChanged      += OnDurationChanged;
            audioService.PlaybackStateChanged += OnPlaybackStateChanged;
            audioService.TrackEnded           += OnTrackEnded;

            //PlaylistService Events
            playlistService.CurrentTrackChanged += OnCurrentTrackChanged;
            playlistService.PlaylistChanged     += OnPlaylistChanged;
            playlistService.TrackIndexChanged   += OnTrackIndexChanged;

            // PlaylistUIService Events
            playlistUIService.PlaylistNameChanged += (s, name) => PlaylistNameText.Text = name;
            playlistUIService.PlaylistModifiedChanged += (s, modified) =>
            {
                var title           = playlistUIService.CurrentPlaylistName;
                if (modified) title += "*";
                PlaylistNameText.Text = title;
            };
            playlistUIService.NotificationRequested += (s, msg) => notificationService.ShowNotification(msg);

            // DragDropService Events
            dragDropService.NotificationRequested += (s, msg) => notificationService.ShowNotification(msg);

            // NotificationService Events
            notificationService.TitleChangeRequested += (s, title) => Title = title;

            // KeyboardService Events
            keyboardService.PlayPauseRequested        += (s, e) => PlayPauseButton_Click(s, e);
            keyboardService.StopRequested             += (s, e) => StopButton_Click(null, null);
            keyboardService.NextTrackRequested        += (s, e) => NextButton_Click(null, null);
            keyboardService.PreviousTrackRequested    += (s, e) => PreviousButton_Click(null, null);
            keyboardService.SeekRequested             += OnSeekRequested;
            keyboardService.VolumeChangeRequested     += OnVolumeChangeRequested;
            keyboardService.MuteToggleRequested       += OnMuteToggleRequested;
            keyboardService.RepeatModeRequested       += (s, e) => RepeatButton_Click(null, null);
            keyboardService.ShuffleToggleRequested    += (s, e) => ShuffleButton_Click(null, null);
            keyboardService.OpenFileRequested         += (s, e) => OpenFileButton_Click(null, null);
            keyboardService.FullscreenToggleRequested += OnFullscreenToggleRequested;
        }

        #region Event Handlers

        // AudioService Handlers
        private void OnPositionChanged(object? sender, TimeSpan position)
        {
            if (!isUserDragging)
            {
                ProgressSlider.Value = position.TotalSeconds;
                CurrentTime.Text     = FormatTime(position);
            }
        }

        private void OnDurationChanged(object? sender, TimeSpan duration)
        {
            ProgressSlider.Maximum = duration.TotalSeconds;
            TotalTime.Text         = FormatTime(duration);
        }

        private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        {
            PlayPauseButton.Content        = isPlaying ? "⏸" : "▶";
            visualizationService.IsEnabled = isPlaying;
        }

        private void OnTrackEnded(object? sender, EventArgs e)
        {
            var nextTrack = playlistService.GetNextTrack();
            if (nextTrack != null)
            {
                audioService.LoadTrack(nextTrack);
                audioService.Play();
            }
            else if (playlistService.PlaybackMode == PlaybackMode.RepeatOne)
            {
                audioService.SetPosition(TimeSpan.Zero);
                audioService.Play();
            }
            else
            {
                audioService.Stop();
            }
        }

        // PlaylistService Handlers
        private void OnCurrentTrackChanged(object? sender, string filePath)
        {
            notificationService.ShowTrackTitle(Path.GetFileNameWithoutExtension(filePath));
        }

        // New Service Handlers
        private void OnPlaylistChanged(object? sender, EventArgs e)
        {
            UpdatePlaylistUI();
        }

        private void OnTrackIndexChanged(object? sender, int index)
        {
            if (PlaylistBox.SelectedIndex != index)
                PlaylistBox.SelectedIndex = index;
        }

        // KeyboardService Handlers
        private void OnSeekRequested(object? sender, int seconds)
        {
            var newPosition = audioService.Position.Add(TimeSpan.FromSeconds(seconds));
            if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
            if (newPosition > audioService.Duration) newPosition = audioService.Duration;

            audioService.SetPosition(newPosition);
        }

        private void OnVolumeChangeRequested(object? sender, double change)
        {
            var newVolume = Math.Max(0, Math.Min(100, VolumeSlider.Value + change));
            VolumeSlider.Value = newVolume;
        }

        private void OnMuteToggleRequested(object? sender, EventArgs e)
        {
            if (VolumeSlider.Value > 0)
            {
                VolumeSlider.Tag   = VolumeSlider.Value;
                VolumeSlider.Value = 0;
            }
            else
            {
                VolumeSlider.Value = VolumeSlider.Tag != null ? (double)VolumeSlider.Tag : 50;
            }
        }

        private void OnFullscreenToggleRequested(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
        }

        #endregion

        #region Helper Methods

        private void UpdatePlaylistUI()
        {
            PlaylistBox.Items.Clear();
            foreach (var track in playlistService.Playlist)
            {
                PlaylistBox.Items.Add(Path.GetFileNameWithoutExtension(track));
            }
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours:D1}:{time.Minutes:D2}:{time.Seconds:D2}";

            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        #endregion

        #region UI Button Handlers

        private void PlayPauseButton_Click(object? sender, EventArgs eventArgs)
        {
            if (playlistService.Count == 0) return;

            if (audioService.IsPlaying)
            {
                audioService.Pause();
            }
            else
            {
                if (playlistService.CurrentTrack != null)
                {
                    audioService.LoadTrack(playlistService.CurrentTrack);
                    audioService.Play();
                }
                else if (playlistService.Count > 0)
                {
                    playlistService.SetCurrentTrack(0);
                    audioService.LoadTrack(playlistService.CurrentTrack);
                    audioService.Play();
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            audioService.Stop();
            notificationService.ShowDefaultTitle();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var nextTrack = playlistService.GetNextTrack();
            if (nextTrack != null)
            {
                audioService.LoadTrack(nextTrack);
                if (audioService.IsPlaying) audioService.Play();
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            var previousTrack = playlistService.GetPreviousTrack();
            if (previousTrack != null)
            {
                audioService.LoadTrack(previousTrack);
                if (audioService.IsPlaying) audioService.Play();
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var currentMode = playlistService.PlaybackMode;
            var newMode = currentMode switch
            {
                PlaybackMode.Normal    => PlaybackMode.RepeatOne,
                PlaybackMode.RepeatOne => PlaybackMode.RepeatAll,
                _                      => PlaybackMode.Normal
            };

            playlistService.SetPlaybackMode(newMode);
            UpdateModeButtons();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            playlistService.SetShuffle(!playlistService.IsShuffleEnabled);
            UpdateModeButtons();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter =
                    "Audio Files (*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                playlistService.AddTracks(dialog.FileNames);
            }
        }

        private void SavePlaylistButton_Click(object  sender, RoutedEventArgs e) => playlistUIService.SavePlaylist();
        private void LoadPlaylistButton_Click(object  sender, RoutedEventArgs e) => playlistUIService.LoadPlaylist();
        private void NewPlaylistButton_Click(object   sender, RoutedEventArgs e) => playlistUIService.NewPlaylist();
        private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e) => playlistUIService.ClearPlaylist();

        #endregion

        #region UI Element Handlers

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioService?.SetVolume(VolumeSlider.Value);
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUserDragging)
                audioService.SetPosition(TimeSpan.FromSeconds(ProgressSlider.Value));
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isUserDragging = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isUserDragging = false;
            audioService.SetPosition(TimeSpan.FromSeconds(ProgressSlider.Value));
        }

        private void PlaylistBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                playlistService.SetCurrentTrack(PlaylistBox.SelectedIndex);
                if (playlistService.CurrentTrack != null)
                {
                    audioService.LoadTrack(playlistService.CurrentTrack);
                    audioService.Play();
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            keyboardService.HandleKeyDown(e.Key);
            e.Handled = true;
        }

        #endregion

        #region Drag & Drop Handlers

        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = dragDropService.HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
                if (dragDropService.HasAudioFiles(files))
                    DragOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = dragDropService.HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_DragLeave(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                dragDropService.AddAudioFiles(files);
            }
        }

        private void PlaylistBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = dragDropService.HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void PlaylistBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = dragDropService.HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void PlaylistBox_DragLeave(object sender, DragEventArgs e)
        {
            // Добавить визуальные эффекты
        }

        private void PlaylistBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                dragDropService.AddAudioFiles(files);
            }
        }

        #endregion

        #region Context Menu Handlers

        private void RemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            contextMenuService.RemoveTrack(PlaylistBox.SelectedIndex);
        }

        private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            contextMenuService.ShowInExplorer(PlaylistBox.SelectedIndex);
        }

        private void TrackProperties_Click(object sender, RoutedEventArgs e)
        {
            contextMenuService.ShowTrackProperties(PlaylistBox.SelectedIndex);
        }

        #endregion

        #region Auto-save and Cleanup

        private void StartAutoSaveTimer()
        {
            autoSaveTimer          =  new DispatcherTimer();
            autoSaveTimer.Interval =  autoSaveInterval;
            autoSaveTimer.Tick     += (s, e) => playlistUIService.AutoSavePlaylist();
            autoSaveTimer.Start();
        }

        private void UpdateModeButtons()
        {
            // Обновляем кнопку Shuffle
            if (playlistService.IsShuffleEnabled)
            {
                ShuffleButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение включено";
            }
            else
            {
                ShuffleButton.Style   = (Style)FindResource("ModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение";
            }

            // Обновляем кнопку Repeat
            switch (playlistService.PlaybackMode)
            {
                case PlaybackMode.RepeatOne:
                    RepeatButton.Content = "🔂";
                    RepeatButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор трека";
                    break;
                case PlaybackMode.RepeatAll:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор плейлиста";
                    break;
                default:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Style   = (Style)FindResource("ModeButtonStyle");
                    RepeatButton.ToolTip = "Режим повтора";
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            autoSaveTimer?.Stop();
            playlistUIService?.AutoSavePlaylist();

            audioService?.Dispose();
            visualizationService?.Dispose();

            base.OnClosed(e);
        }

        #endregion
    }
}