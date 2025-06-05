using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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
            currentTrackIndex = -1;
            random = new Random();
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
                LoadCurrentTrack();
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            if (isPlaying)
                PauseMusic();
            else
                PlayMusic();
        }

        private void PlayMusic() {
            if (playlist.Count == 0)
                return;

            mediaPlayer.Play();
            isPlaying = true;
            PlayPauseButton.Content = "⏸";
            timer.Start();
            visualizationTimer.Start();
        }

        private void PauseMusic() {
            mediaPlayer.Pause();
            isPlaying = false;
            PlayPauseButton.Content = "▶";
            timer.Stop();
            visualizationTimer.Stop();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) {
            mediaPlayer.Stop();
            isPlaying = false;
            PlayPauseButton.Content = "▶";
            timer.Stop();
            visualizationTimer.Stop();
            ProgressSlider.Value = 0;
            CurrentTime.Text = "00:00";
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            currentTrackIndex--;
            if (currentTrackIndex < 0)
                currentTrackIndex = playlist.Count - 1;

            LoadCurrentTrack();
            if (isPlaying)
                PlayMusic();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) {
            if (playlist.Count == 0)
                return;

            currentTrackIndex++;
            if (currentTrackIndex >= playlist.Count)
                currentTrackIndex = 0;

            LoadCurrentTrack();
            if (isPlaying)
                PlayMusic();
        }

        private void LoadCurrentTrack() {
            if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count) {
                string currentTrack = playlist[currentTrackIndex];
                mediaPlayer.Open(new Uri(currentTrack));

                string fileName = System.IO.Path.GetFileNameWithoutExtension(currentTrack);
                TrackTitle.Text = fileName;
                TrackArtist.Text = System.IO.Path.GetDirectoryName(currentTrack);

                PlaylistBox.SelectedIndex = currentTrackIndex;
            }
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                ProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                TotalTime.Text = FormatTime(mediaPlayer.NaturalDuration.TimeSpan);
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e) {
            NextButton_Click(null!, null!);
        }

        private void Timer_Tick(object? sender, EventArgs e) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                ProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                CurrentTime.Text = FormatTime(mediaPlayer.Position);
            }
        }

        private void VisualizationTimer_Tick(object sender, EventArgs e) {
            if (isPlaying) {
                DrawVisualization();
            }
        }

        private void DrawVisualization() {
            VisualizationCanvas.Children.Clear();

            int barCount = 50;
            double canvasWidth = VisualizationCanvas.ActualWidth;
            double canvasHeight = VisualizationCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            double barWidth = canvasWidth / barCount;

            for (int i = 0; i < barCount; i++) {
                double height = random.NextDouble() * canvasHeight * 0.8;

                Rectangle bar = new Rectangle {
                    Width = barWidth - 2,
                    Height = height,
                    Fill = new LinearGradientBrush(
                        Color.FromRgb(0, 160, 255),
                        Color.FromRgb(0, 255, 160),
                        90)
                };

                Canvas.SetLeft(bar, i * barWidth);
                Canvas.SetBottom(bar, 0);

                VisualizationCanvas.Children.Add(bar);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (mediaPlayer is not null) {
                mediaPlayer.Volume = VolumeSlider.Value / 100.0;
            }
        }

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (PlaylistBox.SelectedIndex >= 0 && PlaylistBox.SelectedIndex < playlist.Count) {
                currentTrackIndex = PlaylistBox.SelectedIndex;
                LoadCurrentTrack();
            }
        }

        private string FormatTime(TimeSpan timeSpan) {
            return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
        }

        protected override void OnClosed(EventArgs e) {
            mediaPlayer?.Close();
            timer?.Stop();
            visualizationTimer?.Stop();
            base.OnClosed(e);
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (isUserDragging && mediaPlayer.NaturalDuration.HasTimeSpan) {
                CurrentTime.Text = FormatTime(TimeSpan.FromSeconds(ProgressSlider.Value));
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            isUserDragging = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (mediaPlayer.NaturalDuration.HasTimeSpan) {
                TimeSpan newPosition = TimeSpan.FromSeconds(ProgressSlider.Value);
                mediaPlayer.Position = newPosition;
            }
            isUserDragging = false;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Space:
                    PlayPauseButton_Click(null!, null!);
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
                case Key.Up:
                    VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 10);
                    e.Handled = true;
                    break;
                case Key.Down:
                    VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 10);
                    e.Handled = true;
                    break;
                case Key.S:
                    StopButton_Click(null!, null!);
                    e.Handled = true;
                    break;
                case Key.O:
                    if (Keyboard.Modifiers == ModifierKeys.Control) {
                        OpenFileButton_Click(null!, null!);
                        e.Handled = true;
                    }
                    break;
                case Key.M:
                    ToggleMute();
                    e.Handled = true;
                    break;
                case Key.F:
                    ToggleFullscreen();
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

        private double previousVolume = 50;
        private void ToggleMute() {
            if (VolumeSlider.Value > 0) {
                previousVolume = VolumeSlider.Value;
                VolumeSlider.Value = 0;
            }
            else {
                VolumeSlider.Value = previousVolume;

            }
        }

        private void ToggleFullscreen() {
            if (this.WindowState == WindowState.Maximized) {
                this.WindowState = WindowState.Normal;
            }
            else {
                this.WindowState = WindowState.Maximized;
            }
        }
    }
}