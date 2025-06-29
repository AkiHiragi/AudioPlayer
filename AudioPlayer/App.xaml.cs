using System.Windows;
using AudioPlayer.Services;

namespace AudioPlayer {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public static ThemeService ThemeService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeService = new ThemeService();
            
            base.OnStartup(e);
        }
    }

}
