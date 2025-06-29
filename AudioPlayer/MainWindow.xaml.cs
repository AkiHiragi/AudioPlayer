using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AudioPlayer.Models;
using AudioPlayer.Services;
using Microsoft.Win32;

namespace AudioPlayer
{
    public partial class MainWindow : Window
    {
        // Сервисы
        private AudioService         audioService;
        private PlayListService      playlistService;
        private VisualizationService visualizationService;
        private KeyboardService      keyboardService;

        // UI состояние
        private bool   isUserDragging      = false;
        private string currentPlaylistName = "Новый плейлист";
        private bool   isPlaylistModified  = false;

        // Drag & Drop Support
        private readonly string[] supportedFormats = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac"];

        // Auto-save fields
        private          DateTime lastAutosave     = DateTime.Now;
        private readonly TimeSpan autoSaveInterval = TimeSpan.FromMinutes(2);

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            SetupEventHandlers();

            LoadAutoSavedPlaylist();
            StartAutoSaveTimer();

            this.Loaded += (s, e) => this.Focus();
        }

        private void InitializeServices()
        {
            audioService         = new AudioService();
            playlistService      = new PlayListService();
            visualizationService = new VisualizationService(VisualizationCanvas);
            keyboardService      = new KeyboardService();

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
            audioService.TrackOpened          += OnTrackOpened;

            //PlaylistService Events
            playlistService.CurrentTrackChanged += OnCurrentTrackChanged;
            playlistService.PlaylistChanged     += OnPlaylistChanged;
            playlistService.TrackIndexChanged   += OnTrackIndexChanged;

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

        protected override void OnClosed(EventArgs e)
        {
            AutoSavePlaylist();

            audioService?.Dispose();
            visualizationService?.Dispose();

            base.OnClosed(e);
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

        private void OnTrackOpened(object? sender, EventArgs e)
        {
            // Трек загружен и готов к воспроизведению
        }

        // PlaylistService Handlers
        private void OnCurrentTrackChanged(object? sender, string filePath)
        {
            Title = $"Audio Player - {Path.GetFileNameWithoutExtension(filePath)}";
        }

        private void OnPlaylistChanged(object? sender, EventArgs e)
        {
            UpdatePlaylistUI();
            MarkPlaylistAsModified();
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

        private void MarkPlaylistAsModified()
        {
            if (!isPlaylistModified)
            {
                isPlaylistModified = true;
                UpdatePlaylistTitle();
            }
        }

        private void UpdatePlaylistTitle()
        {
            var title = currentPlaylistName;
            if (isPlaylistModified)
                title += "*";
            PlaylistNameText.Text = title;
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
                    if (audioService.Position == TimeSpan.Zero)
                    {
                        audioService.LoadTrack(playlistService.CurrentTrack);
                    }

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
            Title = "Audio Player";
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var nextTrack = playlistService.GetNextTrack();
            if (nextTrack != null)
            {
                audioService.LoadTrack(nextTrack);
                if (audioService.IsPlaying)
                {
                    audioService.Play();
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            var previousTrack = playlistService.GetPreviousTrack();
            if (previousTrack != null)
            {
                audioService.LoadTrack(previousTrack);
                if (audioService.IsPlaying)
                {
                    audioService.Play();
                }
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var          currentMode = playlistService.PlaybackMode;
            PlaybackMode newMode;

            switch (currentMode)
            {
                case PlaybackMode.Normal:
                    newMode              = PlaybackMode.RepeatOne;
                    RepeatButton.Content = "🔂";
                    RepeatButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор трека";
                    break;

                case PlaybackMode.RepeatOne:
                    newMode              = PlaybackMode.RepeatAll;
                    RepeatButton.Content = "🔁";
                    RepeatButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор плейлиста";
                    break;

                case PlaybackMode.RepeatAll:
                    newMode              = PlaybackMode.Normal;
                    RepeatButton.Content = "🔁";
                    RepeatButton.Style   = (Style)FindResource("ModeButtonStyle");
                    RepeatButton.ToolTip = "Режим повтора";
                    break;

                default:
                    newMode = PlaybackMode.Normal;
                    break;
            }

            playlistService.SetPlaybackMode(newMode);
            MarkPlaylistAsModified();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            var newShuffleState = !playlistService.IsShuffleEnabled;
            playlistService.SetShuffle(newShuffleState);

            if (newShuffleState)
            {
                ShuffleButton.Style   = (Style)FindResource("ActiveModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение включено";
            }
            else
            {
                ShuffleButton.Style   = (Style)FindResource("ModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение";
            }

            MarkPlaylistAsModified();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter =
                    "Audio Files (*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                playlistService.AddTracks(openFileDialog.FileNames);
            }
        }

        private void SavePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title      = "Сохранить плейлист",
                    Filter     = "Плейлисты (*.json)|*.json",
                    DefaultExt = "json",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                    "AudioPlayer", "Playlists")
                };

                if (dialog.ShowDialog() == true)
                {
                    var playlistData = CreatePlaylistData();
                    var fileName     = Path.GetFileNameWithoutExtension(dialog.FileName);
                    playlistData.Name = fileName;

                    PlaylistManager.SavePlaylist(playlistData, dialog.FileName);

                    currentPlaylistName = fileName;
                    isPlaylistModified  = false;
                    UpdatePlaylistTitle();
                    ShowNotification($"Плейлист '{fileName}' сохранен");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void LoadPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title  = "Загрузить плейлист",
                    Filter = "Плейлисты (*.json)|*.json",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                    "AudioPlayer", "Playlists")
                };

                if (dialog.ShowDialog() == true)
                {
                    var playlistData = PlaylistManager.LoadPlaylist(dialog.FileName);
                    if (playlistData != null)
                    {
                        LoadPlaylistData(playlistData);
                        currentPlaylistName = playlistData.Name ?? Path.GetFileNameWithoutExtension(dialog.FileName);
                        isPlaylistModified  = false;
                        UpdatePlaylistTitle();
                        ShowNotification($"Плейлист '{currentPlaylistName}' загружен");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void NewPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            playlistService.Clear();
            audioService.Stop();
            currentPlaylistName = "Новый плейлист";
            isPlaylistModified  = false;
            UpdatePlaylistTitle();
        }

        private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите очистить плейлист?", "Подтвержение",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                playlistService.Clear();
                audioService.Stop();
            }
        }

        #endregion

        #region UI Element Handlers

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

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioService?.SetVolume(VolumeSlider.Value);
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUserDragging)
            {
                audioService.SetPosition(TimeSpan.FromSeconds(ProgressSlider.Value));
            }
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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            keyboardService.HandleKeyDown(e.Key);
            e.Handled = true;
        }

        private PlaylistData CreatePlaylistData()
        {
            var playlistData = new PlaylistData
            {
                Name              = currentPlaylistName,
                CurrentTrackIndex = playlistService.CurrentTrackIndex,
                PlaybackMode      = GetCurrentPlaybackModeToString(),
                IsShuffleEnabled  = playlistService.IsShuffleEnabled,
                Volume            = VolumeSlider.Value
            };

            foreach (var filePath in playlistService.Playlist)
            {
                playlistData.Items.Add(new PlaylistItem(filePath));
            }

            return playlistData;
        }

        private void LoadPlaylistData(PlaylistData playlistData)
        {
            audioService.Stop();
            playlistService.Clear();

            var validTracks = playlistData.Items
                                          .Where(item => File.Exists(item.FilePath))
                                          .Select(item => item.FilePath)
                                          .ToList();

            if (validTracks.Count > 0)
            {
                playlistService.AddTracks(validTracks);

                var trackIndex = Math.Min(playlistData.CurrentTrackIndex, validTracks.Count - 1);
                if (trackIndex >= 0)
                {
                    playlistService.SetCurrentTrack(trackIndex);
                }
            }

            VolumeSlider.Value = playlistData.Volume;

            PlaybackMode mode = playlistData.PlaybackMode switch
            {
                "RepeatOne" => PlaybackMode.RepeatOne,
                "RepeatAll" => PlaybackMode.RepeatAll,
                _           => PlaybackMode.Normal
            };
            playlistService.SetPlaybackMode(mode);
            playlistService.SetShuffle(playlistData.IsShuffleEnabled);

            UpdateModeButtons();
        }

        private string GetCurrentPlaybackModeToString()
        {
            return playlistService.PlaybackMode switch
            {
                PlaybackMode.RepeatOne => "RepeatOne",
                PlaybackMode.RepeatAll => "RepeatAll",
                _                      => "Normal"
            };
        }

        private void UpdateModeButtons()
        {
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

        private void ShowNotification(string message)
        {
            var originalText = Title;
            Title = $"✅ {message}";

            var notificationTimer = new DispatcherTimer();
            notificationTimer.Interval = TimeSpan.FromSeconds(2);
            notificationTimer.Tick += (s, e) =>
            {
                Title = originalText;
                notificationTimer.Stop();
            };
            notificationTimer.Start();
        }

        #endregion

        #region Drag & Drop Handlers

        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
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
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
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
                AddAudioFiles(files);
            }
        }

        private void PlaylistBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Move : DragDropEffects.None;
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
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Move : DragDropEffects.None;
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
                AddAudioFiles(files);
            }
        }

        #endregion

        #region Drag & Drop Helper Methods

        private bool HasAudioFiles(string[] files)
        {
            return files.Any(file =>
            {
                var extension = Path.GetExtension(file).ToLower();
                return supportedFormats.Contains(extension);
            });
        }

        private void AddAudioFiles(string[] files)
        {
            var audioFiles = new List<string>();

            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    var extension = Path.GetExtension(file).ToLower();
                    if (supportedFormats.Contains(extension))
                    {
                        audioFiles.Add(file);
                    }
                }
                else if (Directory.Exists(file))
                {
                    AddAudioFilesFromDirectory(file, audioFiles);
                }
            }

            if (audioFiles.Count > 0)
            {
                playlistService.AddTracks(audioFiles);
            }
        }

        private void AddAudioFilesFromDirectory(string directoryPath, List<string> audioFiles)
        {
            try
            {
                foreach (var extension in supportedFormats)
                {
                    var files = Directory.GetFiles(directoryPath, $"*{extension}", SearchOption.AllDirectories);
                    audioFiles.AddRange(files);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении файлов из папки: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Auto-save methods

        private void LoadAutoSavedPlaylist()
        {
            try
            {
                if (PlaylistManager.AutoSaveExists())
                {
                    var autoSaved = PlaylistManager.LoadPlaylist();
                    if (autoSaved != null && autoSaved.Items.Count > 0)
                    {
                        LoadPlaylistData(autoSaved);
                        
                        if (!string.IsNullOrEmpty(autoSaved.Name) && autoSaved.Name != "AutoSave")
                        {
                            currentPlaylistName = autoSaved.Name;
                        }
                        
                        isPlaylistModified  = false;
                        UpdatePlaylistTitle();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка автозагрузки: {ex.Message}");
            }
        }

        private void StartAutoSaveTimer()
        {
            var autoSaveTimer = new DispatcherTimer();
            autoSaveTimer.Interval =  autoSaveInterval;
            autoSaveTimer.Tick     += (sender, e) => AutoSavePlaylist();
            autoSaveTimer.Start();
        }

        private void AutoSavePlaylist()
        {
            try
            {
                if (playlistService.Count > 0 && (isPlaylistModified || DateTime.Now - lastAutosave > autoSaveInterval))
                {
                    var playlistData = CreatePlaylistData();
                    playlistData.Name = "AutoSave";
                    PlaylistManager.SavePlaylist(playlistData);
                    lastAutosave = DateTime.Now;
                }
            }
            catch
            {
                // Игнорируем ошибки автозагрузки
            }
        }

        #endregion

        #region ContextMenu Handlers

        private void RemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                var selectedIndex = PlaylistBox.SelectedIndex;
                var trackName     = Path.GetFileNameWithoutExtension(playlistService.Playlist[selectedIndex]);

                var result = MessageBox.Show(
                    $"Удалить трек '{trackName}' из плейлиста?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var wasCurrentTrack = selectedIndex == playlistService.CurrentTrackIndex;
                    playlistService.RemoveTrack(selectedIndex);

                    if (wasCurrentTrack)
                    {
                        audioService.Stop();
                    }
                }
            }
        }

        private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                var filePath = playlistService.Playlist[PlaylistBox.SelectedIndex];

                if (File.Exists(filePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть проводник: {ex.Message}",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Файл не найден.", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void TrackProperties_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                var filePath = playlistService.Playlist[PlaylistBox.SelectedIndex];

                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    var info = $"Файл: {fileInfo.Name}\n"                            +
                               $"Путь: {fileInfo.DirectoryName}\n"                   +
                               $"Размер: {fileInfo.Length / 1024 / 1024:F1} МБ\n"    +
                               $"Создан: {fileInfo.CreationTime:dd.MM.yyyy HH:mm}\n" +
                               $"Изменен: {fileInfo.LastWriteTime:dd.MM.yyyy HH:mm}";

                    MessageBox.Show(info, "Свойства трека",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Файл не найден.", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion
    }
}