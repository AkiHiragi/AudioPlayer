using System.IO;
using System.Windows;

namespace AudioPlayer.Services;

public class ContextMenuService
{
    private PlayListService playlistService;
    private AudioService audioService;
    
    public ContextMenuService(PlayListService playlistService, AudioService audioService)
    {
        this.playlistService = playlistService;
        this.audioService = audioService;
    }
    
    public void RemoveTrack(int selectedIndex)
    {
        if (selectedIndex >= 0 && selectedIndex < playlistService.Count)
        {
            string trackName = Path.GetFileNameWithoutExtension(playlistService.Playlist[selectedIndex]);
            
            var result = MessageBox.Show(
                $"Удалить трек '{trackName}' из плейлиста?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                bool wasCurrentTrack = selectedIndex == playlistService.CurrentTrackIndex;
                playlistService.RemoveTrack(selectedIndex);
                
                if (wasCurrentTrack)
                {
                    audioService.Stop();
                }
            }
        }
    }
    
    public void ShowInExplorer(int selectedIndex)
    {
        if (selectedIndex >= 0 && selectedIndex < playlistService.Count)
        {
            string filePath = playlistService.Playlist[selectedIndex];
            
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
                MessageBox.Show("Файл не найден", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
    
    public void ShowTrackProperties(int selectedIndex)
    {
        if (selectedIndex >= 0 && selectedIndex < playlistService.Count)
        {
            string filePath = playlistService.Playlist[selectedIndex];
            
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                string info = $"Файл: {fileInfo.Name}\n" +
                             $"Путь: {fileInfo.DirectoryName}\n" +
                             $"Размер: {fileInfo.Length / 1024 / 1024:F1} МБ\n" +
                             $"Создан: {fileInfo.CreationTime:dd.MM.yyyy HH:mm}\n" +
                             $"Изменен: {fileInfo.LastWriteTime:dd.MM.yyyy HH:mm}";
                             
                MessageBox.Show(info, "Свойства трека", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Файл не найден", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}