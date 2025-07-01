using System;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
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
        private ThemeService         themeService;
        private TrayService          trayService;
        private CompactModeService   compactModeService;

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

            themeService = App.ThemeService;

            trayService        = new TrayService(this);
            compactModeService = new CompactModeService(this);

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

            // TrayService Events
            trayService.ShowRequested          += (s, e) => ShowFromTray();
            trayService.HideRequested          += (s, e) => HideToTray();
            trayService.ExitRequested          += (s, e) => Application.Current.Shutdown();
            trayService.PlayPauseRequested     += (s, e) => PlayPauseButton_Click(null, null);
            trayService.NextTrackRequested     += (s, e) => NextButton_Click(null, null);
            trayService.PreviousTrackRequested += (s, e) => PreviousButton_Click(null, null);

            // CompactModeService Events
            compactModeService.CompactModeChanged += OnCompactModeChanged;
        }

        #region Event Handlers

        // AudioService Handlers
        private void OnPositionChanged(object? sender, TimeSpan position)
        {
            if (!isUserDragging)
            {
                ProgressSlider.Value = position.TotalSeconds;
                CurrentTime.Text     = FormatTime(position);
                
                if (FindName("CompactProgressSlider") is Slider compactProgress)
                {
                    compactProgress.Value = position.TotalSeconds;
                }
            }
        }

        private void OnDurationChanged(object? sender, TimeSpan duration)
        {
            ProgressSlider.Maximum = duration.TotalSeconds;
            TotalTime.Text         = FormatTime(duration);
            
            if (FindName("CompactProgressSlider") is Slider compactProgress)
            {
                compactProgress.Maximum = duration.TotalSeconds;
            }
        }

        private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        {
            if (PlayPauseButton.Content is PackIcon playPauseIcon)
            {
                playPauseIcon.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
            }

            if (FindName("CompactPlayPauseButton") is Button compactButton &&
                compactButton.Content is PackIcon compactIcon)
            {
                compactIcon.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
            }

            PlayPauseButton.ToolTip        = isPlaying ? "Пауза" : "Воспроизведение";
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
                    if (audioService.Duration == TimeSpan.Zero)
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

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            themeService.ToggleTheme();

            var themeIcon = ThemeIcon;
            if (themeService.IsDarkTheme)
            {
                themeIcon.Kind            = PackIconKind.WeatherNight;
                ThemeToggleButton.ToolTip = "Переключить на светлую тему";
            }
            else
            {
                themeIcon.Kind            = PackIconKind.WeatherSunny;
                ThemeToggleButton.ToolTip = "Переключить на темную тему";
            }
        }

        private void CompactModeButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("CompactModeButton clicked");
            compactModeService.ToggleCompactMode();
        }


        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e) =>
            HideToTray();

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
            var activeColor = (Color)ColorConverter.ConvertFromString("#4CAF50");
            var normalColor = Colors.Gray;

            // Обновляем кнопку Shuffle
            if (playlistService.IsShuffleEnabled)
            {
                ShuffleButton.Foreground = new SolidColorBrush(activeColor);
                ShuffleButton.ToolTip    = "Случайное воспроизведение включено";
            }
            else
            {
                ShuffleButton.Foreground = new SolidColorBrush(normalColor);
                ShuffleButton.ToolTip    = "Случайное воспроизведение";
            }

            // Обновляем кнопку Repeat
            switch (playlistService.PlaybackMode)
            {
                case PlaybackMode.RepeatOne:
                    RepeatButton.Foreground = new SolidColorBrush(activeColor);
                    RepeatButton.ToolTip    = "Повтор трека";
                    break;
                case PlaybackMode.RepeatAll:
                    RepeatButton.Foreground = new SolidColorBrush(activeColor);
                    RepeatButton.ToolTip    = "Повтор плейлиста";
                    break;
                default:
                    RepeatButton.Foreground = new SolidColorBrush(normalColor);
                    RepeatButton.ToolTip    = "Режим повтора";
                    break;
            }
        }

        #endregion

        #region Tray and CompactMode

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            trayService.HideTrayIcon();
        }

        private void HideToTray()
        {
            Hide();
            trayService.ShowTrayIcon();
            trayService.ShowBalloonTip("Audio Player", "Приложение свернуто в трей");
        }

        private void OnCompactModeChanged(object? sender, bool isCompact)
        {
            var mainGrid     = Content as Grid;
            var compactPanel = FindName("CompactModePanel") as UIElement;

            if (isCompact)
            {
                // Убираем отступы
                if (mainGrid != null) mainGrid.Margin = new Thickness(4);

                // Скрываем основные элементы
                var menuCard     = FindName("MenuCard") as UIElement;
                var vizCard      = FindName("VisualizationCard") as UIElement;
                var playlistCard = FindName("PlaylistCard") as UIElement;

                if (menuCard     != null) menuCard.Visibility     = Visibility.Collapsed;
                if (vizCard      != null) vizCard.Visibility      = Visibility.Collapsed;
                if (playlistCard != null) playlistCard.Visibility = Visibility.Collapsed;

                // Скрываем обычные панели управления
                foreach (UIElement child in mainGrid.Children)
                {
                    int row = Grid.GetRow(child);
                    if (row == 3 || row == 4) // Прогресс и управление
                    {
                        child.Visibility = Visibility.Collapsed;
                    }
                }

                // Показываем компактную панель
                if (compactPanel != null)
                {
                    compactPanel.Visibility = Visibility.Visible;
                    SyncCompactControls();
                }

                // Обнуляем высоты скрытых строк
                if (mainGrid != null && mainGrid.RowDefinitions.Count >= 6)
                {
                    mainGrid.RowDefinitions[0].Height = new GridLength(0);
                    mainGrid.RowDefinitions[1].Height = new GridLength(0);
                    mainGrid.RowDefinitions[2].Height = new GridLength(0);
                    mainGrid.RowDefinitions[3].Height = new GridLength(0);
                    mainGrid.RowDefinitions[4].Height = new GridLength(0);
                }
            }
            else
            {
                // Возвращаем обычный режим
                if (mainGrid != null) mainGrid.Margin = new Thickness(16);

                // Показываем основные элементы
                var menuCard     = FindName("MenuCard") as UIElement;
                var vizCard      = FindName("VisualizationCard") as UIElement;
                var playlistCard = FindName("PlaylistCard") as UIElement;

                if (menuCard     != null) menuCard.Visibility     = Visibility.Visible;
                if (vizCard      != null) vizCard.Visibility      = Visibility.Visible;
                if (playlistCard != null) playlistCard.Visibility = Visibility.Visible;

                // Показываем обычные панели управления
                foreach (UIElement child in mainGrid.Children)
                {
                    int row = Grid.GetRow(child);
                    if (row == 3 || row == 4) // Прогресс и управление
                    {
                        child.Visibility = Visibility.Visible;
                    }
                }

                // Скрываем компактную панель
                if (compactPanel != null) compactPanel.Visibility = Visibility.Collapsed;

                // Возвращаем исходные высоты строк
                if (mainGrid != null && mainGrid.RowDefinitions.Count >= 6)
                {
                    mainGrid.RowDefinitions[0].Height = GridLength.Auto;
                    mainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                    mainGrid.RowDefinitions[2].Height = new GridLength(200);
                    mainGrid.RowDefinitions[3].Height = GridLength.Auto;
                    mainGrid.RowDefinitions[4].Height = GridLength.Auto;
                }

                // Возвращаем отступы
                if (menuCard is FrameworkElement menu) menu.Margin             = new Thickness(0, 0, 0, 16);
                if (vizCard is FrameworkElement viz) viz.Margin                = new Thickness(0, 0, 0, 16);
                if (playlistCard is FrameworkElement playlist) playlist.Margin = new Thickness(0, 0, 0, 16);
            }
        }

        private void SyncCompactControls()
        {
            // Синхронизируем прогресс
            if (FindName("CompactProgressSlider") is Slider compactProgress)
            {
                compactProgress.Value   = ProgressSlider.Value;
                compactProgress.Maximum = ProgressSlider.Maximum;
                
                compactProgress.ValueChanged -= CompactProgressSlider_ValueChanged;
                compactProgress.ValueChanged += CompactProgressSlider_ValueChanged;
        
                compactProgress.PreviewMouseLeftButtonDown -= CompactProgressSlider_PreviewMouseLeftButtonDown;
                compactProgress.PreviewMouseLeftButtonDown += CompactProgressSlider_PreviewMouseLeftButtonDown;
        
                compactProgress.PreviewMouseLeftButtonUp -= CompactProgressSlider_PreviewMouseLeftButtonUp;
                compactProgress.PreviewMouseLeftButtonUp += CompactProgressSlider_PreviewMouseLeftButtonUp;
        
                compactProgress.IsMoveToPointEnabled = true;
            }
            
            if (FindName("CompactVolumeSlider") is Slider compactVolume)
            {
                compactVolume.Value        =  VolumeSlider.Value;
                compactVolume.ValueChanged -= CompactVolumeSlider_ValueChanged;
                compactVolume.ValueChanged += CompactVolumeSlider_ValueChanged;
            }
            
            if (FindName("CompactPlayPauseButton") is Button compactPlayPause)
            {
                if (PlayPauseButton.Content is PackIcon mainIcon && 
                    compactPlayPause.Content is PackIcon compactIcon)
                {
                    compactIcon.Kind = mainIcon.Kind;
                }
            }
            
            if (FindName("CompactTrackName") is TextBlock trackName)
            {
                trackName.Text = playlistService.CurrentTrack != null 
                                     ? Path.GetFileNameWithoutExtension(playlistService.CurrentTrack)
                                     : "Нет трека";
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                HideToTray();
            base.OnStateChanged(e);
        }

        #endregion

        #region Compact Mode Handlers

        private void CompactProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUserDragging && sender is Slider compactSlider)
            {
                audioService.SetPosition(TimeSpan.FromSeconds(compactSlider.Value));
            }
        }

        private void CompactProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isUserDragging = true;
        }

        private void CompactProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider compactSlider)
            {
                isUserDragging = false;
                audioService.SetPosition(TimeSpan.FromSeconds(compactSlider.Value));
            }
        }

        private void CompactVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider compactVolume)
            {
                VolumeSlider.Value = compactVolume.Value;
                audioService?.SetVolume(compactVolume.Value);
            }
        }

        #endregion
        
        protected override void OnClosed(EventArgs e)
        {
            autoSaveTimer?.Stop();
            playlistUIService?.AutoSavePlaylist();

            audioService?.Dispose();
            visualizationService?.Dispose();

            trayService?.Dispose();

            base.OnClosed(e);
        }
    }
}