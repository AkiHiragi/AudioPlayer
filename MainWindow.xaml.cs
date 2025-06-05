using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;

namespace AudioPlayer
{
    public partial class MainWindow : Window
    {
        private IWavePlayer wavePlayer;
        private WaveStream waveStream;
        private string currentFilePath;
        private DispatcherTimer timer;
        private bool isPlaying = false;
        private readonly int fftLength = 1024;
        private Complex[] fftBuffer;
        private byte[] audioBuffer;
        private WaveFormat waveFormat;

        public MainWindow()
        {
            InitializeComponent();
            
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(30); // 30ms для обновления визуализации
            timer.Tick += Timer_Tick;
            
            fftBuffer = new Complex[fftLength];
            audioBuffer = new byte[fftLength * 4]; // 4 байта на сэмпл (стерео, 16 бит)
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Аудио файлы|*.mp3;*.wav|Все файлы|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private void LoadFile(string filePath)
        {
            Stop();
            
            try
            {
                // Освобождаем предыдущие ресурсы
                if (wavePlayer != null)
                {
                    wavePlayer.Dispose();
                    wavePlayer = null;
                }
                
                if (waveStream != null)
                {
                    waveStream.Dispose();
                    waveStream = null;
                }
                
                // Используем WaveChannel32 для MP3 файлов
                if (filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    waveStream = new Mp3FileReader(filePath);
                }
                else // Для WAV и других поддерживаемых форматов
                {
                    waveStream = new AudioFileReader(filePath);
                }
                
                waveFormat = waveStream.WaveFormat;
                
                // Используем WaveOut вместо MediaFoundation
                wavePlayer = new WaveOut();
                wavePlayer.Init(waveStream);
                
                currentFilePath = filePath;
                currentTrackText.Text = System.IO.Path.GetFileName(filePath);
                
                Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void Play()
        {
            if (wavePlayer != null && !isPlaying)
            {
                wavePlayer.Play();
                timer.Start();
                isPlaying = true;
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying && wavePlayer != null)
            {
                wavePlayer.Pause();
                timer.Stop();
                isPlaying = false;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void Stop()
        {
            if (wavePlayer != null)
            {
                wavePlayer.Stop();
            }
            
            if (waveStream != null)
            {
                waveStream.Position = 0;
            }
            
            timer.Stop();
            isPlaying = false;
            visualizationCanvas.Children.Clear();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (wavePlayer != null && wavePlayer is WaveOut waveOut)
            {
                waveOut.Volume = (float)e.NewValue;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (waveStream != null && isPlaying)
            {
                // Обновление времени воспроизведения
                TimeSpan currentTime = TimeSpan.FromSeconds((double)waveStream.Position / waveStream.WaveFormat.AverageBytesPerSecond);
                TimeSpan totalTime = waveStream.TotalTime;
                currentTimeText.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
                
                // Визуализация
                UpdateVisualization();
            }
        }

        private void UpdateVisualization()
        {
            if (waveStream == null || !isPlaying)
                return;

            // Сохраняем текущую позицию
            long position = waveStream.Position;
            
            // Читаем аудио данные для анализа
            waveStream.Position = waveStream.Position - (waveStream.Position % waveFormat.BlockAlign);
            int bytesRead = waveStream.Read(audioBuffer, 0, audioBuffer.Length);
            
            // Восстанавливаем позицию
            waveStream.Position = position;
            
            if (bytesRead == 0)
                return;

            // Преобразуем байты в комплексные числа для FFT
            for (int i = 0; i < fftLength && i * 4 < bytesRead; i++)
            {
                // Берем только левый канал для простоты
                short sample = (short)((audioBuffer[i * 4] | (audioBuffer[i * 4 + 1] << 8)));
                fftBuffer[i].X = (float)(sample / 32768.0); // Нормализация
                fftBuffer[i].Y = 0;
            }

            // Выполняем быстрое преобразование Фурье
            FastFourierTransform.FFT(true, 10, fftBuffer); // 2^10 = 1024

            // Очищаем холст
            visualizationCanvas.Children.Clear();

            // Рисуем визуализацию
            int barCount = 64; // Количество полос
            double barWidth = visualizationCanvas.ActualWidth / barCount;
            
            for (int i = 0; i < barCount; i++)
            {
                // Используем логарифмическую шкалу для частот
                int fftIndex = (int)Math.Pow(i, 2) / barCount;
                if (fftIndex >= fftLength / 2) fftIndex = fftLength / 2 - 1;
                
                // Вычисляем амплитуду
                double amplitude = Math.Sqrt(fftBuffer[fftIndex].X * fftBuffer[fftIndex].X + 
                                           fftBuffer[fftIndex].Y * fftBuffer[fftIndex].Y);
                
                // Масштабируем и ограничиваем высоту
                double height = Math.Min(amplitude * visualizationCanvas.ActualHeight * 2, visualizationCanvas.ActualHeight);
                
                // Создаем прямоугольник для визуализации
                Rectangle bar = new Rectangle
                {
                    Width = barWidth - 1,
                    Height = height,
                    Fill = new LinearGradientBrush(
                        Colors.Blue,
                        Colors.Cyan,
                        90)
                };

                // Размещаем прямоугольник на холсте
                Canvas.SetLeft(bar, i * barWidth);
                Canvas.SetTop(bar, visualizationCanvas.ActualHeight - height);
                visualizationCanvas.Children.Add(bar);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Освобождаем ресурсы
            if (wavePlayer != null)
            {
                wavePlayer.Dispose();
                wavePlayer = null;
            }

            if (waveStream != null)
            {
                waveStream.Dispose();
                waveStream = null;
            }

            base.OnClosed(e);
        }
    }

    // Класс для работы с комплексными числами для FFT
    public struct Complex
    {
        public float X;
        public float Y;
    }

    // Класс для быстрого преобразования Фурье
    public class FastFourierTransform
    {
        public static void FFT(bool forward, int m, Complex[] data)
        {
            int n = 1 << m;
            
            // Бит-реверсивная перестановка
            int i, j;
            for (i = 0; i < n; i++)
            {
                j = BitReverse(i, m);
                if (j > i)
                {
                    var temp = data[i];
                    data[i] = data[j];
                    data[j] = temp;
                }
            }

            // Вычисление FFT
            int blockEnd = 1;
            int blockSize;
            
            for (blockSize = 2; blockSize <= n; blockSize <<= 1)
            {
                float deltaAngle = (forward ? -2.0f : 2.0f) * (float)Math.PI / blockSize;
                float sin1 = (float)Math.Sin(-2 * Math.PI / blockSize);
                float sin2 = (float)Math.Sin(-Math.PI / blockSize);
                float cos2 = (float)Math.Cos(Math.PI / blockSize);
                float wtemp = (float)Math.Sin(0.5 * deltaAngle);
                float wpr = -2.0f * wtemp * wtemp;
                float wpi = (float)Math.Sin(deltaAngle);
                float wr = 1.0f;
                float wi = 0.0f;

                for (i = 0; i < n; i += blockSize)
                {
                    int blockOffset = i;
                    for (j = 0; j < blockEnd; j++)
                    {
                        int offset1 = blockOffset;
                        int offset2 = blockOffset + blockEnd;
                        
                        float tr = wr * data[offset2].X - wi * data[offset2].Y;
                        float ti = wr * data[offset2].Y + wi * data[offset2].X;
                        
                        data[offset2].X = data[offset1].X - tr;
                        data[offset2].Y = data[offset1].Y - ti;
                        data[offset1].X += tr;
                        data[offset1].Y += ti;
                        
                        blockOffset++;
                    }
                    
                    wtemp = wr;
                    wr = wtemp * cos2 - wi * sin2 + wtemp * wpr - wi * wpi;
                    wi = wi * cos2 + wtemp * sin2 + wi * wpr + wtemp * wpi;
                }
                blockEnd = blockSize;
            }

            // Нормализация (для обратного преобразования)
            if (!forward)
            {
                for (i = 0; i < n; i++)
                {
                    data[i].X /= n;
                    data[i].Y /= n;
                }
            }
        }

        private static int BitReverse(int n, int bits)
        {
            int reversedN = n;
            int count = bits - 1;

            n >>= 1;
            while (n > 0)
            {
                reversedN = (reversedN << 1) | (n & 1);
                count--;
                n >>= 1;
            }

            return ((reversedN << count) & ((1 << bits) - 1));
        }
    }
}