using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using static AudioPlayer.MainWindow;

namespace AudioPlayer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private MediaPlayer mediaPlayer;
        private DispatcherTimer timer;
        private DispatcherTimer visualizationTimer;
        private List<string> playlist;
        private int currentTrackIndex;
        private bool isPlaying;
        private Random random;
        private bool isUserDragging = false;

        private DateTime lastLeftKeyPress = DateTime.MinValue;
        private DateTime lastRightKeyPress = DateTime.MinValue;
        private readonly TimeSpan doubleClickInterval = TimeSpan.FromMilliseconds(400);

        public enum PlaybackMode {
            Normal,
            RepeatOne,
            RepeatAll
        }

        private PlaybackMode currentPlaybackMode = PlaybackMode.Normal;
        private bool isShuffleEnabled = false;
        private List<int> shuffleOrder;
        private int shuffleIndex = 0;

        public MainWindow() {
            InitializeComponent();
            InitializePlayer();

            this.Loaded += (s, e) => this.Focus();
        }

        private void InitializePlayer() {
            mediaPlayer = new MediaPlayer();
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            visualizationTimer = new DispatcherTimer();
            visualizationTimer.Interval = TimeSpan.FromMicroseconds(50);
            visualizationTimer.Tick += VisualizationTimer_Tick;

            playlist = new List<string>();
            random = new Random();
            currentTrackIndex = -1;

            CurrentTime.Text = "00:00";
            TotalTime.Text = "00:00";
        }

        private void Timer_Tick(object? sender, EventArgs e) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan && !isUserDragging) {
                var totalSeconds = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                var currentSeconds = mediaPlayer.Position.TotalSeconds;

                ProgressSlider.Maximum = totalSeconds;
                ProgressSlider.Value = currentSeconds;

                CurrentTime.Text = FormatTime(mediaPlayer.Position);
                TotalTime.Text = FormatTime(mediaPlayer.NaturalDuration.TimeSpan);
            }
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                ProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                TotalTime.Text = FormatTime(mediaPlayer.NaturalDuration.TimeSpan);
                CurrentTime.Text = "00:00";
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e) {
            switch (currentPlaybackMode) {
                case PlaybackMode.RepeatOne:
                    mediaPlayer.Position = TimeSpan.Zero;
                    mediaPlayer.Play();
                    break;

                case PlaybackMode.Normal:
                case PlaybackMode.RepeatAll:
                    if (isShuffleEnabled) {
                        PlayNextShuffled();
                    }
                    else {
                        PlayNextNormal();
                    }
                    break;
            }
        }

        private void PlayNextNormal() {
            if (currentTrackIndex < playlist.Count - 1) {
                currentTrackIndex++;
                PlayCurrentTrack();
            }
            else if (currentPlaybackMode == PlaybackMode.RepeatAll) {
                currentTrackIndex = 0;
                PlayCurrentTrack();
            }
            else {
                StopButton_Click(null!, null!);
            }
        }

        private void PlayNextShuffled() {
            if (shuffleOrder == null || shuffleOrder.Count == 0) {
                GenerateShuffleOrder();
            }
            shuffleIndex++;
            if (shuffleIndex >= shuffleOrder.Count) {
                if (currentPlaybackMode == PlaybackMode.RepeatAll) {
                    shuffleIndex = 0;
                }
                else {
                    StopButton_Click(null!, null!);
                    return;
                }
            }

            currentTrackIndex = shuffleOrder[shuffleIndex];
            PlayCurrentTrack();
        }

        private void GenerateShuffleOrder() {
            shuffleOrder = Enumerable.Range(0, playlist.Count).ToList();

            for (int i = shuffleOrder.Count - 1; i > 0; i--) {
                int j = random.Next(i + 1);
                int temp = shuffleOrder[i];
                shuffleOrder[i] = shuffleOrder[j];
                shuffleOrder[j] = temp;
            }

            shuffleIndex = shuffleOrder.IndexOf(currentTrackIndex);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e) {
            switch (currentPlaybackMode) {
                case PlaybackMode.Normal:
                    currentPlaybackMode = PlaybackMode.RepeatOne;
                    RepeatButton.Content = "🔂"; // Повтор одного
                    RepeatButton.Style = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор трека";
                    break;

                case PlaybackMode.RepeatOne:
                    currentPlaybackMode = PlaybackMode.RepeatAll;
                    RepeatButton.Content = "🔁"; // Повтор всех
                    RepeatButton.Style = (Style)FindResource("ActiveModeButtonStyle");
                    RepeatButton.ToolTip = "Повтор плейлиста";
                    break;

                case PlaybackMode.RepeatAll:
                    currentPlaybackMode = PlaybackMode.Normal;
                    RepeatButton.Content = "🔁";
                    RepeatButton.Style = (Style)FindResource("ModeButtonStyle");
                    RepeatButton.ToolTip = "Режим повтора";
                    break;
            }
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e) {
            isShuffleEnabled = !isShuffleEnabled;

            if (isShuffleEnabled) {
                ShuffleButton.Style = (Style)FindResource("ActiveModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение включено";
                GenerateShuffleOrder();
            }
            else {
                ShuffleButton.Style = (Style)FindResource("ModeButtonStyle");
                ShuffleButton.ToolTip = "Случайное воспроизведение";
                shuffleOrder = null!;
            }
        }

        private void PlayCurrentTrack() {
            if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count) {
                try {
                    mediaPlayer.Open(new Uri(playlist[currentTrackIndex]));
                    mediaPlayer.Play();
                    isPlaying = true;
                    PlayPauseButton.Content = "⏸";

                    timer.Start();
                    visualizationTimer.Start();

                    PlaylistBox.SelectedIndex = currentTrackIndex;

                    Title = $"Audio Player - {System.IO.Path.GetFileNameWithoutExtension(playlist[currentTrackIndex])}";
                }
                catch (Exception ex) {
                    MessageBox.Show($"Ошибка воспроизведения: {ex.Message}");
                }
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() is true) {
                foreach (string fileName in openFileDialog.FileNames) {
                    playlist.Add(fileName);
                    PlaylistBox.Items.Add(System.IO.Path.GetFileNameWithoutExtension(fileName));
                }
            }

            if (currentTrackIndex == -1 && playlist.Count > 0) {
                currentTrackIndex = 0;
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            if (isPlaying) {
                mediaPlayer.Pause();
                isPlaying = false;
                PlayPauseButton.Content = "▶";
                timer.Stop();
                visualizationTimer.Stop();
            }
            else {
                if (currentTrackIndex == -1) {
                    currentTrackIndex = 0;
                    PlayCurrentTrack();
                }
                else {
                    mediaPlayer.Play();
                    isPlaying = true;
                    PlayPauseButton.Content = "⏸";
                    timer.Start();
                    visualizationTimer.Start();
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) {
            mediaPlayer.Stop();
            isPlaying = false;
            PlayPauseButton.Content = "▶";
            timer.Stop();
            visualizationTimer.Stop();
            ProgressSlider.Value = 0;
            CurrentTime.Text = "00:00";
            Title = "Audio Player";
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            if (isShuffleEnabled) {
                PlayNextShuffled();
            }
            else {
                if (currentTrackIndex < playlist.Count - 1) {
                    currentTrackIndex++;
                    PlayCurrentTrack();
                }
                else if (currentPlaybackMode == PlaybackMode.RepeatAll) {
                    currentTrackIndex = 0;
                    PlayCurrentTrack();
                }
            }
        }


        private void PreviousButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            if (isShuffleEnabled) {
                if (shuffleOrder != null && shuffleIndex > 0) {
                    shuffleIndex--;
                    currentTrackIndex = shuffleOrder[shuffleIndex];
                    PlayCurrentTrack();
                }
                else {
                    if (currentTrackIndex > 0) {
                        currentTrackIndex--;
                        PlayCurrentTrack();
                    }
                    else if (currentPlaybackMode == PlaybackMode.RepeatAll) {
                        currentTrackIndex = playlist.Count - 1;
                        PlayCurrentTrack();
                    }
                }
            }
        }

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (PlaylistBox.SelectedIndex >= 0 && PlaylistBox.SelectedIndex < playlist.Count) {
                currentTrackIndex = PlaylistBox.SelectedIndex;
                PlayCurrentTrack();
            }

            if (isShuffleEnabled && shuffleOrder != null) {
                shuffleIndex = shuffleOrder.IndexOf(currentTrackIndex);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (mediaPlayer is not null) {
                mediaPlayer.Volume = VolumeSlider.Value / 100.0;
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (isUserDragging && mediaPlayer.NaturalDuration.HasTimeSpan) {
                mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            isUserDragging = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            isUserDragging = false;
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        private void VisualizationTimer_Tick(object sender, EventArgs e) {
            VisualizationCanvas.Children.Clear();

            int barCount = 20;
            double barWidth = VisualizationCanvas.ActualWidth / barCount;

            for (int i = 0; i < barCount; i++) {
                double height = random.NextDouble() * VisualizationCanvas.ActualHeight * 0.8;

                Rectangle bar = new Rectangle {
                    Width = barWidth - 2,
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromRgb(
                        (byte)(100 + random.Next(155)),
                        (byte)(150 + random.Next(105)),
                        (byte)(200 + random.Next(55))
                        ))
                };

                Canvas.SetLeft(bar, i * bar.Width);
                Canvas.SetBottom(bar, 0);

                VisualizationCanvas.Children.Add(bar);
            }
        }

        private string FormatTime(TimeSpan timeSpan) {
            if (timeSpan.TotalHours >= 1) {
                return $"{(int)timeSpan.TotalHours:D1}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else {
                return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Space:
                    PlayPauseButton_Click(null!, null!);
                    e.Handled = true;
                    break;

                case Key.S:
                    StopButton_Click(null!, null!);
                    e.Handled = true;
                    break;

                case Key.M:
                    if (VolumeSlider.Value > 0) {
                        VolumeSlider.Tag = VolumeSlider.Value; // Сохраняем текущую громкость
                        VolumeSlider.Value = 0;
                    }
                    else {
                        VolumeSlider.Value = VolumeSlider.Tag != null ? (double)VolumeSlider.Tag : 50;
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 10);
                    e.Handled = true;
                    break;

                case Key.Down:
                    VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 10);
                    e.Handled = true;
                    break;

                case Key.Left:
                    HandleLeftKey();
                    e.Handled = true;
                    break;

                case Key.Right:
                    HandleRightKey();
                    e.Handled = true;
                    break;

                case Key.O:
                    if (Keyboard.Modifiers == ModifierKeys.Control) {
                        OpenFileButton_Click(null!, null!);
                        e.Handled = true;
                    }
                    break;

                case Key.F:
                    if (WindowState == WindowState.Normal) {
                        WindowState = WindowState.Maximized;
                        WindowStyle = WindowStyle.None;
                    }
                    else {
                        WindowState = WindowState.Normal;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    e.Handled = true;
                    break;

                case Key.R:
                    RepeatButton_Click(null!, null!);
                    e.Handled = true;
                    break;

                case Key.H:
                    ShuffleButton_Click(null!, null!);
                    e.Handled = true;
                    break;

            }
        }

        private void HandleLeftKey() {
            DateTime now = DateTime.Now;
            if (now - lastLeftKeyPress < doubleClickInterval) {
                PreviousButton_Click(null!, null!);
                lastLeftKeyPress = DateTime.MinValue;
            }
            else {
                SeekBackward(10);
                lastLeftKeyPress = now;
            }
        }

        private void HandleRightKey() {
            DateTime now = DateTime.Now;
            if (now - lastRightKeyPress < doubleClickInterval) {
                NextButton_Click(null!, null!);
                lastRightKeyPress = DateTime.MinValue;
            }
            else {
                SeekForward(10);
                lastRightKeyPress = now;
            }
        }

        private void SeekForward(int seconds) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                TimeSpan newPosition = mediaPlayer.Position.Add(TimeSpan.FromSeconds(seconds));
                TimeSpan maxPosition = mediaPlayer.NaturalDuration.TimeSpan;

                if (newPosition > maxPosition)
                    newPosition = maxPosition;

                mediaPlayer.Position = newPosition;
                ProgressSlider.Value = newPosition.TotalSeconds;
            }
        }

        private void SeekBackward(int seconds) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                TimeSpan newPosition = mediaPlayer.Position.Subtract(TimeSpan.FromSeconds(seconds));
                TimeSpan minPosition = TimeSpan.Zero;

                if (newPosition < minPosition)
                    newPosition = minPosition;

                mediaPlayer.Position = newPosition;
                ProgressSlider.Value = newPosition.TotalSeconds;
            }
        }

        protected override void OnClosed(EventArgs e) {
            mediaPlayer?.Close();
            timer?.Stop();
            visualizationTimer?.Stop();

            mediaPlayer = null!;
            timer = null!;
            visualizationTimer = null!;

            base.OnClosed(e);
        }

        #region Drag & Drop Support

        private readonly string[] supportedFormats = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac"];

        private void MainWindow_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (HasAudioFiles(files)) {
                    e.Effects = DragDropEffects.Copy;
                    ShowDragOverlay();
                }
                else {
                    e.Effects = DragDropEffects.None;
                }
            }
            else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_DragLeave(object sender, DragEventArgs e) {
            HideDragOverlay();
        }

        private void MainWindow_Drop(object sender, DragEventArgs e) {
            HideDragOverlay();

            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddAudioFiles(files);
            }
        }

        private void PlaylistBox_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;

                if (HasAudioFiles(files)) {
                    PlaylistBox.Background = new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0xA0, 0xFF));
                }
            }
            else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void PlaylistBox_DragOver(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = HasAudioFiles(files) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void PlaylistBox_DragLeave(object sender, DragEventArgs e) {
            PlaylistBox.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        }

        private void PlaylistBox_Drop(object sender, DragEventArgs e) {
            PlaylistBox.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddAudioFiles(files);
            }
        }
                
        private bool HasAudioFiles(string[] files) {
            return files.Any(file => {
                string extension = System.IO.Path.GetExtension(file).ToLower();
                return supportedFormats.Contains(extension);
            });
        }

        private void AddAudioFiles(string[] files) {
            int addedCount = 0;

            foreach (string file in files) {
                if (File.Exists(file)) {
                    string extension = System.IO.Path.GetExtension(file).ToLower();
                    if (supportedFormats.Contains(extension)) {
                        if (!playlist.Contains(file)) {
                            playlist.Add(file);
                            PlaylistBox.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                            addedCount++;
                        }
                    }
                }
                else if (Directory.Exists(file)) {
                    AddAudioFilesFromDirectory(file, ref addedCount);
                }
            }

            if (addedCount > 0) {
                if (currentTrackIndex == -1 && playlist.Count > 0) {
                    currentTrackIndex = 0;
                }

                ShowNotification($"Добавлено треков: {addedCount}");

                if (isShuffleEnabled) {
                    GenerateShuffleOrder();
                }
            }
        }

        private void AddAudioFilesFromDirectory(string directoryPath, ref int addedCount) {
            try {
                foreach (string extension in supportedFormats) {
                    string[] files = Directory.GetFiles(directoryPath, $"*{extension}", 
                        SearchOption.AllDirectories);

                    foreach (string file in files) {
                        if (!playlist.Contains(file)) {
                            playlist.Add(file);
                            PlaylistBox.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                            addedCount++;
                        }
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при добавлении файлов из папки: {ex.Message}",
                       "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowDragOverlay() {
            DragOverlay.Visibility = Visibility.Visible;
        }

        private void HideDragOverlay() {
            DragOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowNotification(string message) {
            // Простое уведомление в заголовке окна
            string originalTitle = Title;
            Title = $"✅ {message}";

            // Возвращаем оригинальный заголовок через 2 секунды
            var notificationTimer = new DispatcherTimer();
            notificationTimer.Interval = TimeSpan.FromSeconds(2);
            notificationTimer.Tick += (s, e) =>
            {
                Title = originalTitle;
                notificationTimer.Stop();
            };
            notificationTimer.Start();
        }

        #endregion
    }
}