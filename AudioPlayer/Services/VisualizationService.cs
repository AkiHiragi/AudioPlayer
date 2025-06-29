using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AudioPlayer.Services;

public class VisualizationService
{
    private DispatcherTimer visualizationTimer;
    private Canvas          canvas;
    private Random          random;
    private bool            isEnabled;

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            isEnabled = value;
            if (value)
                Start();
            else
                Stop();
        }
    }

    public VisualizationService(Canvas visualizationCanvas)
    {
        canvas = visualizationCanvas;
        random = new Random();

        visualizationTimer          =  new DispatcherTimer();
        visualizationTimer.Interval =  TimeSpan.FromMilliseconds(50);
        visualizationTimer.Tick     += OnVisualizationTick;
    }

    private void Start()
    {
        if (isEnabled)
            visualizationTimer.Start();
    }

    private void Stop()
    {
        visualizationTimer.Stop();
        ClearVisualization();
    }

    private void OnVisualizationTick(object? sender, EventArgs e)
    {
        if (!isEnabled || canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0)
        {
            canvas.Children.Clear();
            return;
        }

        canvas.Children.Clear();

        var barCount = 20;
        var barWidth = canvas.ActualWidth / barCount;

        for (var i = 0; i < barCount; i++)
        {
            var height = random.NextDouble() * canvas.ActualHeight * 0.8;

            var bar = new Rectangle
            {
                Width  = barWidth - 2,
                Height = height,
                Fill = new SolidColorBrush(Color.FromRgb(
                                               (byte)(100 + random.Next(155)),
                                               (byte)(150 + random.Next(105)),
                                               (byte)(200 + random.Next(55))
                                           ))
            };

            Canvas.SetLeft(bar, i * barWidth);
            Canvas.SetBottom(bar, 0);

            canvas.Children.Add(bar);
        }
    }

    private void ClearVisualization()
    {
        canvas.Children.Clear();
    }

    public void Dispose()
    {
        Stop();
        visualizationTimer = null;
    }
}