using System.IO;
using System.Windows;
using AudioPlayer.Models;
using Microsoft.Win32;

namespace AudioPlayer.Services;

public class PlaylistUIService
{
    private PlayListService playlistService;
    private AudioService audioService;
    
    // События для уведомления UI
    public event EventHandler<string> PlaylistNameChanged;
    public event EventHandler<bool> PlaylistModifiedChanged;
    public event EventHandler<string> NotificationRequested;
    
    public string CurrentPlaylistName { get; private set; } = "Новый плейлист";
    public bool IsPlaylistModified { get; private set; } = false;
    
    public PlaylistUIService(PlayListService playlistService, AudioService audioService)
    {
        this.playlistService = playlistService;
        this.audioService = audioService;
        
        // Подписываемся на изменения плейлиста
        playlistService.PlaylistChanged += OnPlaylistChanged;
    }
    
    private void OnPlaylistChanged(object sender, EventArgs e)
    {
        MarkAsModified();
    }
    
    public void SavePlaylist()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Сохранить плейлист",
                Filter = "Плейлисты (*.json)|*.json",
                DefaultExt = "json",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AudioPlayer", "Playlists")
            };

            if (dialog.ShowDialog() == true)
            {
                var playlistData = CreatePlaylistData();
                string fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                playlistData.Name = fileName;

                PlaylistManager.SavePlaylist(playlistData, dialog.FileName);

                CurrentPlaylistName = fileName;
                IsPlaylistModified = false;
                
                PlaylistNameChanged?.Invoke(this, CurrentPlaylistName);
                PlaylistModifiedChanged?.Invoke(this, IsPlaylistModified);
                NotificationRequested?.Invoke(this, $"Плейлист '{fileName}' сохранен");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    public void LoadPlaylist()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Загрузить плейлист",
                Filter = "Плейлисты (*.json)|*.json",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AudioPlayer", "Playlists")
            };

            if (dialog.ShowDialog() == true)
            {
                var playlistData = PlaylistManager.LoadPlaylist(dialog.FileName);
                if (playlistData != null)
                {
                    LoadPlaylistData(playlistData);
                    CurrentPlaylistName = playlistData.Name ?? Path.GetFileNameWithoutExtension(dialog.FileName);
                    IsPlaylistModified = false;
                    
                    PlaylistNameChanged?.Invoke(this, CurrentPlaylistName);
                    PlaylistModifiedChanged?.Invoke(this, IsPlaylistModified);
                    NotificationRequested?.Invoke(this, $"Плейлист '{CurrentPlaylistName}' загружен");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    public void NewPlaylist()
    {
        if (IsPlaylistModified)
        {
            var result = MessageBox.Show(
                "Текущий плейлист был изменен. Сохранить изменения?",
                "Несохраненные изменения",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SavePlaylist();
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        audioService.Stop();
        playlistService.Clear();
        
        CurrentPlaylistName = "Новый плейлист";
        IsPlaylistModified = false;
        
        PlaylistNameChanged?.Invoke(this, CurrentPlaylistName);
        PlaylistModifiedChanged?.Invoke(this, IsPlaylistModified);
        NotificationRequested?.Invoke(this, "Создан новый плейлист");
    }
    
    public void ClearPlaylist()
    {
        if (playlistService.Count == 0) return;

        var result = MessageBox.Show(
            "Вы уверены, что хотите очистить плейлист?",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            audioService.Stop();
            playlistService.Clear();
            NotificationRequested?.Invoke(this, "Плейлист очищен");
        }
    }
    
    public void LoadAutoSavedPlaylist()
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
                        CurrentPlaylistName = autoSaved.Name;
                    }
                    
                    IsPlaylistModified = false;
                    PlaylistNameChanged?.Invoke(this, CurrentPlaylistName);
                    PlaylistModifiedChanged?.Invoke(this, IsPlaylistModified);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка автозагрузки: {ex.Message}");
        }
    }
    
    public void AutoSavePlaylist()
    {
        try
        {
            if (playlistService.Count > 0)
            {
                var playlistData = CreatePlaylistData();
                playlistData.Name = CurrentPlaylistName;
                PlaylistManager.SavePlaylist(playlistData);
            }
        }
        catch
        {
            // Игнорируем ошибки автосохранения
        }
    }
    
    private void MarkAsModified()
    {
        if (!IsPlaylistModified)
        {
            IsPlaylistModified = true;
            PlaylistModifiedChanged?.Invoke(this, IsPlaylistModified);
        }
    }
    
    private PlaylistData CreatePlaylistData()
    {
        var playlistData = new PlaylistData
        {
            Name = CurrentPlaylistName,
            CurrentTrackIndex = playlistService.CurrentTrackIndex,
            PlaybackMode = GetCurrentPlaybackModeString(),
            IsShuffleEnabled = playlistService.IsShuffleEnabled,
            Volume = 50 // Будет передаваться извне
        };

        foreach (string filePath in playlistService.Playlist)
        {
            playlistData.Items.Add(new PlaylistItem(filePath));
        }

        return playlistData;
    }
    
    private void LoadPlaylistData(PlaylistData playlistData)
    {
        audioService.Stop();
        playlistService.Clear();

        var validTracks = new List<string>();
        foreach (var item in playlistData.Items)
        {
            if (File.Exists(item.FilePath))
            {
                validTracks.Add(item.FilePath);
            }
        }

        if (validTracks.Count > 0)
        {
            playlistService.AddTracks(validTracks);
            
            int trackIndex = Math.Min(playlistData.CurrentTrackIndex, validTracks.Count - 1);
            if (trackIndex >= 0)
            {
                playlistService.SetCurrentTrack(trackIndex);
            }
        }
        
        PlaybackMode mode = playlistData.PlaybackMode switch
        {
            "RepeatOne" => PlaybackMode.RepeatOne,
            "RepeatAll" => PlaybackMode.RepeatAll,
            _ => PlaybackMode.Normal
        };
        playlistService.SetPlaybackMode(mode);
        playlistService.SetShuffle(playlistData.IsShuffleEnabled);
    }
    
    private string GetCurrentPlaybackModeString()
    {
        return playlistService.PlaybackMode switch
        {
            PlaybackMode.RepeatOne => "RepeatOne",
            PlaybackMode.RepeatAll => "RepeatAll",
            _ => "Normal"
        };
    }
    
    public void SetVolume(double volume)
    {
        // Метод для установки громкости при сохранении
    }
}